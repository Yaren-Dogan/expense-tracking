using ExpenseTracker.Domain.Enums;
using ExpenseTracker.Domain.Insights;

namespace ExpenseTracker.Application.Features.Insights.Plans;

/// <summary>A category the planner (model or rules) chose to trim, before the numbers are worked out.</summary>
public sealed record CutChoice(ExpenseCategory Category, CutLevel Level, string Reason);

/// <summary>The worked-out plan: how long, how much a month, and each category's target.</summary>
public sealed record PlanFigures(int Months, decimal MonthlySaving, decimal MonthlyBudget, IReadOnlyList<PlanCut> Cuts);

/// <summary>
/// Every number in a budget plan is computed here. The planner only says which categories to trim and how hard
/// (light / medium / strong); this class turns that into amounts that add up to the goal, extends the plan when the goal
/// does not fit, and keeps essentials (bills, health) untouched.
/// </summary>
public static class PlanMath
{
    /// <summary>Categories a plan may trim. Bills and health are commitments, not choices.</summary>
    public static readonly IReadOnlySet<ExpenseCategory> Flexible = new HashSet<ExpenseCategory>
    {
        ExpenseCategory.Market, ExpenseCategory.Food, ExpenseCategory.Transport,
        ExpenseCategory.Entertainment, ExpenseCategory.Clothing, ExpenseCategory.Other
    };

    /// <summary>No category is cut by more than half: a plan that cannot be lived with is not followed.</summary>
    public const decimal MaxCutRatio = 0.5m;

    public static decimal Ratio(CutLevel level) => level switch
    {
        CutLevel.Light => 0.10m,
        CutLevel.Medium => 0.20m,
        _ => 0.35m
    };

    /// <summary>The level that describes a ratio after scaling, so the label matches the amount shown.</summary>
    public static CutLevel LevelOf(decimal ratio) => ratio <= 0.125m ? CutLevel.Light : ratio <= 0.275m ? CutLevel.Medium : CutLevel.Strong;

    /// <summary>The largest saving possible per month: every flexible category cut by half.</summary>
    public static decimal MaxMonthlySaving(IReadOnlyDictionary<ExpenseCategory, decimal> baseline) =>
        baseline.Where(kv => Flexible.Contains(kv.Key)).Sum(kv => kv.Value) * MaxCutRatio;

    /// <returns>Null when the goal cannot be met within <paramref name="maxMonths"/> even with every flexible category halved.</returns>
    public static PlanFigures? Compute(
        decimal goal, int requestedMonths, int maxMonths,
        IReadOnlyDictionary<ExpenseCategory, decimal> baseline, IReadOnlyList<CutChoice> choices)
    {
        var maxSaving = MaxMonthlySaving(baseline);
        if (maxSaving <= 0)
            return null;

        // The monthly saving is rounded up to whole liras, so the check uses the rounded figure: 301 over 3 months is 101
        // a month, which a 100,50 ceiling cannot carry even though 301 ÷ 3 = 100,33 would fit.
        var months = requestedMonths;
        while (Math.Ceiling(goal / months) > maxSaving)
        {
            if (++months > maxMonths)
                return null;
        }

        var saving = Math.Ceiling(goal / months);
        var cuts = Distribute(saving, baseline, choices);
        // The budget follows the goal exactly; the category targets show where that saving is meant to come from.
        var monthlyBudget = RoundDown(baseline.Values.Sum() - saving);
        return new PlanFigures(months, saving, monthlyBudget, cuts);
    }

    /// <summary>
    /// Spreads the monthly saving over the chosen categories in proportion to their levels, capped at half of each.
    /// When the chosen ones cannot carry it, the other flexible categories join, largest first.
    /// </summary>
    private static List<PlanCut> Distribute(
        decimal saving, IReadOnlyDictionary<ExpenseCategory, decimal> baseline, IReadOnlyList<CutChoice> choices)
    {
        var picks = choices
            .Where(c => Flexible.Contains(c.Category) && baseline.GetValueOrDefault(c.Category) > 0)
            .DistinctBy(c => c.Category)
            .ToList();
        var extras = baseline
            .Where(kv => Flexible.Contains(kv.Key) && kv.Value > 0 && picks.All(p => p.Category != kv.Key))
            .OrderByDescending(kv => kv.Value)
            .Select(kv => new CutChoice(kv.Key, CutLevel.Light, "Hedefe ulaşmak için bu kalemden de biraz kısmak gerekiyor."))
            .ToList();

        while (true)
        {
            var weights = picks.ToDictionary(p => p.Category, p => baseline[p.Category] * Ratio(p.Level));
            var amounts = Allocate(saving, weights, baseline);
            if (amounts.Values.Sum() >= saving - 0.01m || extras.Count == 0)
                return picks.Select(p =>
                {
                    var current = baseline[p.Category];
                    var cut = amounts[p.Category];
                    // Targets round up to whole tens, so the savings shown never add up to more than the plan asks for.
                    return new PlanCut(p.Category, LevelOf(cut / current), decimal.Round(current, 2), RoundUp(current - cut), p.Reason);
                }).Where(c => c.CurrentMonthly > c.TargetMonthly).ToList();

            picks.Add(extras[0]);
            extras.RemoveAt(0);
        }
    }

    /// <summary>Proportional allocation with a per-category cap; whatever a capped category cannot take moves to the others.</summary>
    private static Dictionary<ExpenseCategory, decimal> Allocate(
        decimal saving, Dictionary<ExpenseCategory, decimal> weights, IReadOnlyDictionary<ExpenseCategory, decimal> baseline)
    {
        var result = weights.Keys.ToDictionary(k => k, _ => 0m);
        var open = weights.Keys.ToHashSet();
        var remaining = saving;
        while (remaining > 0.01m && open.Count > 0)
        {
            var weightSum = open.Sum(k => weights[k]);
            if (weightSum <= 0)
                break;

            var spentThisRound = 0m;
            foreach (var k in open.ToList())
            {
                var cap = baseline[k] * MaxCutRatio - result[k];
                var share = Math.Min(cap, remaining * weights[k] / weightSum);
                result[k] += share;
                spentThisRound += share;
                if (cap - share <= 0.01m)
                    open.Remove(k);
            }

            remaining -= spentThisRound;
            if (spentThisRound <= 0.01m)
                break;
        }

        return result;
    }

    private static decimal RoundDown(decimal amount) => Math.Floor(amount / 10m) * 10m;

    private static decimal RoundUp(decimal amount) => Math.Ceiling(amount / 10m) * 10m;
}
