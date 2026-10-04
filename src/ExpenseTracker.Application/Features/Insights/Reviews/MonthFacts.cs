using ExpenseTracker.Application.Common.Ai;
using ExpenseTracker.Application.Common.Text;
using ExpenseTracker.Domain.Enums;
using ExpenseTracker.Domain.ValueObjects;
using ExpenseTracker.Domain.Weather;

namespace ExpenseTracker.Application.Features.Insights.Reviews;

/// <summary>One expense as the review sees it.</summary>
public sealed record SpendingRow(Guid Id, decimal Amount, ExpenseCategory Category, DateOnly SpentOn, string? Note, Guid UserId);

/// <summary>
/// Something worth noticing about a month, as a finished Turkish sentence. <paramref name="Score"/> ranks facts when
/// no model is there to choose (or its choice is unusable).
/// </summary>
public sealed record ReviewFact(string Id, string Sentence, int Score);

/// <summary>
/// Everything with a number in a month review is computed here, never by the model: totals, changes against last month,
/// the biggest expense, who spent how much. The review agent picks among these facts and words the text around them.
/// </summary>
public static class MonthFacts
{
    private const decimal MinChange = 250m;
    private const decimal MinChangeRatio = 0.25m;

    public static IReadOnlyList<ReviewFact> Compute(
        BudgetMonth month,
        decimal? budget,
        IReadOnlyList<SpendingRow> expenses,
        IReadOnlyDictionary<ExpenseCategory, decimal> previousByCategory,
        IReadOnlyDictionary<Guid, string> names)
    {
        var facts = new List<ReviewFact>();
        if (expenses.Count == 0)
            return facts;

        var monthName = TurkishText.MonthName(month.Month);
        var total = expenses.Sum(e => e.Amount);
        var previousTotal = previousByCategory.Values.Sum();

        facts.Add(new ReviewFact("total", budget is { } b
            ? total <= b
                ? $"{monthName} ayında toplam {Money(total)} harcandı; {Money(b)} bütçeyle ay {Money(b - total)} artıyla kapandı."
                : $"{monthName} ayında toplam {Money(total)} harcandı; {Money(b)} bütçe {Money(total - b)} aşıldı."
            : $"{monthName} ayında toplam {Money(total)} harcandı (bu ay için bütçe girilmemişti).", 100));

        if (previousTotal > 0 && Math.Abs(total - previousTotal) / previousTotal >= 0.10m)
        {
            var change = Percent(Math.Abs(total - previousTotal) / previousTotal);
            facts.Add(new ReviewFact("total-change", total > previousTotal
                ? $"Toplam harcama bir önceki aya göre %{change} arttı ({Money(previousTotal)} → {Money(total)})."
                : $"Toplam harcama bir önceki aya göre %{change} azaldı ({Money(previousTotal)} → {Money(total)}).", 60));
        }

        var byCategory = expenses.GroupBy(e => e.Category).ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));
        // Without a previous month (the ledger is new) every category would look "new": compare only when there is one.
        var comparable = previousTotal > 0 ? byCategory.Keys.Union(previousByCategory.Keys) : [];
        foreach (var category in comparable)
        {
            var now = byCategory.GetValueOrDefault(category);
            var before = previousByCategory.GetValueOrDefault(category);
            var diff = now - before;
            var name = CategoryNames.Turkish(category);
            if (Math.Abs(diff) < MinChange)
                continue;

            if (before == 0)
                facts.Add(new ReviewFact($"cat-new:{category}",
                    $"{name} kategorisinde bir önceki ay harcama yokken bu ay {Money(now)} harcandı.", 45 + Weight(now, total)));
            else if (now == 0)
                facts.Add(new ReviewFact($"cat-gone:{category}",
                    $"{name} kategorisinde bir önceki ay {Money(before)} harcanmıştı, bu ay hiç harcanmadı.", 35 + Weight(before, total)));
            else if (Math.Abs(diff) / before >= MinChangeRatio)
                facts.Add(new ReviewFact(diff > 0 ? $"cat-up:{category}" : $"cat-down:{category}", diff > 0
                    ? $"{name} harcaması bir önceki aya göre %{Percent(diff / before)} arttı ({Money(before)} → {Money(now)})."
                    : $"{name} harcaması bir önceki aya göre %{Percent(-diff / before)} azaldı ({Money(before)} → {Money(now)}).",
                    (diff > 0 ? 50 : 40) + Weight(Math.Abs(diff), total)));
        }

        var top = byCategory.MaxBy(kv => kv.Value);
        facts.Add(new ReviewFact("top",
            $"En büyük kalem {CategoryNames.Turkish(top.Key)}: {Money(top.Value)}, toplamın %{Percent(top.Value / total)} kadarı.", 45));

        var biggest = expenses.MaxBy(e => e.Amount)!;
        if (expenses.Count >= 3 && biggest.Amount >= total * 0.25m)
            facts.Add(new ReviewFact("biggest",
                $"Tek seferde en büyük harcama: {Describe(biggest)}, {Money(biggest.Amount)} ({TurkishText.Date(biggest.SpentOn)}).", 40));

        var byPerson = expenses.GroupBy(e => e.UserId).Select(g => (Id: g.Key, Total: g.Sum(e => e.Amount))).OrderByDescending(p => p.Total).ToList();
        if (byPerson.Count >= 2)
            facts.Add(new ReviewFact("members", "Harcamaların " + string.Join(", ",
                byPerson.Select(p => $"%{Percent(p.Total / total)} kadarını {names.GetValueOrDefault(p.Id) ?? "eski bir üye"}")) + " girdi.", 35));

        var quietDays = month.DaysInMonth - expenses.Select(e => e.SpentOn).Distinct().Count();
        if (quietDays >= 5)
            facts.Add(new ReviewFact("quiet-days", $"Ayın {quietDays} günü hiç harcama yapılmadı.", 20));

        return facts.OrderByDescending(f => f.Score).ToList();
    }

    /// <summary>The month's sky, using the same thresholds as the daily weather (spent ÷ budget).</summary>
    public static WeatherCondition? Sky(BudgetMonth month, decimal? budget, decimal total) =>
        budget is { } b ? BudgetWeather.ForecastMonth(b, total, month.LastDay).Condition : null;

    private static int Weight(decimal amount, decimal total) => total == 0 ? 0 : (int)Math.Min(30, amount / total * 100);

    private static string Describe(SpendingRow e) =>
        string.IsNullOrWhiteSpace(e.Note) ? CategoryNames.Turkish(e.Category) : $"{e.Note} ({CategoryNames.Turkish(e.Category)})";

    private static string Money(decimal amount) => TurkishText.MoneyShort(amount);

    private static int Percent(decimal ratio) => (int)Math.Round(ratio * 100, MidpointRounding.AwayFromZero);
}
