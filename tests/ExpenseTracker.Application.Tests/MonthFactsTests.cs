using ExpenseTracker.Application.Features.Insights.Reviews;
using ExpenseTracker.Domain.Enums;
using ExpenseTracker.Domain.ValueObjects;
using ExpenseTracker.Domain.Weather;
using FluentAssertions;

namespace ExpenseTracker.Application.Tests;

public class MonthFactsTests
{
    private static readonly BudgetMonth August = new(2026, 8);
    private static readonly Guid Yaren = Guid.NewGuid(), Ahmet = Guid.NewGuid();
    private static readonly Dictionary<Guid, string> Names = new() { [Yaren] = "Yaren", [Ahmet] = "Ahmet" };

    private static SpendingRow Row(decimal amount, ExpenseCategory category, int day, string? note = null, Guid? user = null) =>
        new(Guid.NewGuid(), amount, category, new DateOnly(2026, 8, day), note, user ?? Yaren);

    [Fact]
    public void Total_is_compared_with_the_budget()
    {
        var facts = MonthFacts.Compute(August, 30000, [Row(15000, ExpenseCategory.Bills, 6), Row(9510, ExpenseCategory.Market, 10)],
            new Dictionary<ExpenseCategory, decimal>(), Names);

        facts[0].Should().Be(new ReviewFact("total",
            "Ağustos ayında toplam ₺24.510 harcandı; ₺30.000 bütçeyle ay ₺5.490 artıyla kapandı.", 100));
    }

    [Fact]
    public void Big_category_changes_are_found_small_ones_ignored()
    {
        var previous = new Dictionary<ExpenseCategory, decimal>
        {
            [ExpenseCategory.Transport] = 900, [ExpenseCategory.Food] = 1000, [ExpenseCategory.Clothing] = 600
        };
        var facts = MonthFacts.Compute(August, null,
            [Row(1440, ExpenseCategory.Transport, 3), Row(1100, ExpenseCategory.Food, 4), Row(400, ExpenseCategory.Entertainment, 5)],
            previous, Names);

        facts.Should().Contain(f => f.Id == "cat-up:Transport" && f.Sentence == "Ulaşım harcaması bir önceki aya göre %60 arttı (₺900 → ₺1.440).");
        facts.Should().Contain(f => f.Id == "cat-new:Entertainment");
        facts.Should().Contain(f => f.Id == "cat-gone:Clothing");
        facts.Should().NotContain(f => f.Id.EndsWith(":Food"), "₺100 is below the ₺250 threshold");
    }

    [Fact]
    public void A_first_month_has_no_category_comparisons()
    {
        var facts = MonthFacts.Compute(August, null, [Row(15000, ExpenseCategory.Bills, 6), Row(4000, ExpenseCategory.Market, 9)],
            new Dictionary<ExpenseCategory, decimal>(), Names);

        facts.Should().NotContain(f => f.Id.StartsWith("cat-"), "with no previous month every category would look new");
    }

    [Fact]
    public void Household_split_and_quiet_days_are_reported()
    {
        var facts = MonthFacts.Compute(August, null,
            [Row(600, ExpenseCategory.Market, 1, user: Yaren), Row(400, ExpenseCategory.Food, 2, user: Ahmet)],
            new Dictionary<ExpenseCategory, decimal>(), Names);

        facts.Should().Contain(f => f.Id == "members" && f.Sentence == "Harcamaların %60 kadarını Yaren, %40 kadarını Ahmet girdi.");
        facts.Should().Contain(f => f.Id == "quiet-days" && f.Sentence == "Ayın 29 günü hiç harcama yapılmadı.");
    }

    [Fact]
    public void The_months_sky_uses_the_daily_weather_thresholds()
    {
        MonthFacts.Sky(August, 30000, 24510).Should().Be(WeatherCondition.PartlyCloudy);
        MonthFacts.Sky(August, 30000, 50000).Should().Be(WeatherCondition.Stormy);
        MonthFacts.Sky(August, null, 1000).Should().BeNull();
    }
}
