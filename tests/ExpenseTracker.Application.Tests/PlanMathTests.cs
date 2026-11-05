using ExpenseTracker.Application.Features.Insights.Plans;
using ExpenseTracker.Domain.Enums;
using ExpenseTracker.Domain.Insights;
using FluentAssertions;

namespace ExpenseTracker.Application.Tests;

public class PlanMathTests
{
    private static readonly Dictionary<ExpenseCategory, decimal> Baseline = new()
    {
        [ExpenseCategory.Bills] = 15000, [ExpenseCategory.Market] = 4000, [ExpenseCategory.Food] = 3000,
        [ExpenseCategory.Entertainment] = 1000, [ExpenseCategory.Health] = 500
    };

    [Fact]
    public void The_saving_is_spread_over_the_chosen_categories_by_level()
    {
        var plan = PlanMath.Compute(4000, 4, 24, Baseline,
            [new CutChoice(ExpenseCategory.Food, CutLevel.Medium, "a"), new CutChoice(ExpenseCategory.Entertainment, CutLevel.Strong, "b")])!;

        plan.Months.Should().Be(4);
        plan.MonthlySaving.Should().Be(1000);
        plan.MonthlyBudget.Should().Be(22500, "baseline 23.500 minus 1.000");
        // Weights: food 3000×0.20 = 600, entertainment 1000×0.35 = 350 → 1000 split 600:350.
        plan.Cuts.Select(c => (c.Category, c.TargetMonthly)).Should().Equal(
            (ExpenseCategory.Food, 2370m), (ExpenseCategory.Entertainment, 640m));
    }

    [Fact]
    public void Essentials_are_never_cut_and_caps_pull_in_other_flexible_categories()
    {
        var plan = PlanMath.Compute(9000, 3, 24, Baseline,
            [new CutChoice(ExpenseCategory.Bills, CutLevel.Strong, "x"), new CutChoice(ExpenseCategory.Entertainment, CutLevel.Strong, "y")])!;

        plan.Cuts.Should().NotContain(c => c.Category == ExpenseCategory.Bills || c.Category == ExpenseCategory.Health);
        plan.Cuts.Should().OnlyContain(c => c.TargetMonthly >= c.CurrentMonthly / 2, "no category is cut by more than half");
        plan.Cuts.Sum(c => c.CurrentMonthly - c.TargetMonthly).Should().BeInRange(2970, 3000, "3.000 a month; targets round up to tens");
    }

    [Fact]
    public void The_rounded_up_monthly_saving_never_exceeds_what_the_flexible_categories_can_give()
    {
        // Market 201 → at most 100,50 a month. 301 over 3 months is 100,33, but the saving rounds up to 101: too much.
        // 4 months: 301 ÷ 4 = 75,25 → 76, which fits.
        var baseline = new Dictionary<ExpenseCategory, decimal> { [ExpenseCategory.Market] = 201 };

        var plan = PlanMath.Compute(301, 3, 24, baseline, [])!;

        plan.Months.Should().Be(4);
        plan.MonthlySaving.Should().Be(76);
        plan.MonthlySaving.Should().BeLessThanOrEqualTo(PlanMath.MaxMonthlySaving(baseline));
    }

    [Fact]
    public void A_goal_that_does_not_fit_is_stretched_over_more_months_or_rejected()
    {
        // Flexible total 8.000 → at most 4.000 a month.
        var stretched = PlanMath.Compute(20000, 2, 24, Baseline, [])!;
        stretched.Months.Should().Be(5);
        stretched.MonthlySaving.Should().Be(4000);

        PlanMath.Compute(200000, 2, 24, Baseline, []).Should().BeNull("50 months is over the limit");
        PlanMath.Compute(1000, 2, 24, new Dictionary<ExpenseCategory, decimal> { [ExpenseCategory.Bills] = 5000 }, []).Should().BeNull();
    }

    [Theory]
    [InlineData(0.10, CutLevel.Light)]
    [InlineData(0.20, CutLevel.Medium)]
    [InlineData(0.40, CutLevel.Strong)]
    public void The_label_follows_the_real_ratio(double ratio, CutLevel expected) =>
        PlanMath.LevelOf((decimal)ratio).Should().Be(expected);
}
