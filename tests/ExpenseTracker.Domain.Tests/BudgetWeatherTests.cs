using ExpenseTracker.Domain.Weather;
using FluentAssertions;

namespace ExpenseTracker.Domain.Tests;

public class BudgetWeatherTests
{
    // September has 30 days: a 3000 budget on the 1st with nothing spent yields an allowance of exactly 100.
    private static readonly DateOnly FirstOfSeptember = new(2026, 9, 1);

    [Theory]
    [InlineData("0", WeatherCondition.Sunny)]
    [InlineData("0.5", WeatherCondition.Sunny)]
    [InlineData("0.51", WeatherCondition.PartlyCloudy)]
    [InlineData("0.9", WeatherCondition.PartlyCloudy)]
    [InlineData("0.91", WeatherCondition.Cloudy)]
    [InlineData("1.0", WeatherCondition.Cloudy)]
    [InlineData("1.01", WeatherCondition.Rainy)]
    [InlineData("1.5", WeatherCondition.Rainy)]
    [InlineData("1.51", WeatherCondition.Stormy)]
    public void Classify_uses_prd_thresholds(string ratio, WeatherCondition expected) =>
        BudgetWeather.Classify(decimal.Parse(ratio, System.Globalization.CultureInfo.InvariantCulture)).Should().Be(expected);

    [Theory]
    [InlineData(0, WeatherCondition.Sunny)]
    [InlineData(50, WeatherCondition.Sunny)]
    [InlineData(51, WeatherCondition.PartlyCloudy)]
    [InlineData(100, WeatherCondition.Cloudy)]
    [InlineData(150, WeatherCondition.Rainy)]
    [InlineData(151, WeatherCondition.Stormy)]
    public void ForDay_compares_spending_with_the_daily_allowance(int spentToday, WeatherCondition expected)
    {
        var weather = BudgetWeather.ForDay(3000m, 0m, spentToday, FirstOfSeptember);

        weather.DailyAllowance.Should().Be(100m);
        weather.Condition.Should().Be(expected);
    }

    [Fact]
    public void ForDay_spreads_the_remaining_budget_over_the_remaining_days_including_today()
    {
        // 1000 already spent on the 1st..10th; 2000 left over 21 days (11th..30th + today).
        var weather = BudgetWeather.ForDay(3000m, 1000m, 0m, new DateOnly(2026, 9, 10));

        weather.DailyAllowance.Should().Be(95.24m);
        weather.SafeToSpend.Should().Be(95.24m);
    }

    [Fact]
    public void ForDay_safe_to_spend_never_goes_negative()
    {
        var weather = BudgetWeather.ForDay(3000m, 0m, 250m, FirstOfSeptember);

        weather.SafeToSpend.Should().Be(0m);
        weather.Ratio.Should().Be(2.5m);
    }

    [Fact]
    public void ForDay_is_stormy_when_the_budget_is_gone_and_money_is_still_spent()
    {
        var weather = BudgetWeather.ForDay(3000m, 3000m, 10m, new DateOnly(2026, 9, 20));

        weather.Condition.Should().Be(WeatherCondition.Stormy);
        weather.DailyAllowance.Should().Be(0m);
        weather.Ratio.Should().BeNull();
    }

    [Fact]
    public void ForDay_is_cloudy_when_the_budget_is_gone_but_nothing_is_spent_today()
    {
        BudgetWeather.ForDay(3000m, 3200m, 0m, new DateOnly(2026, 9, 20))
            .Condition.Should().Be(WeatherCondition.Cloudy);
    }

    [Fact]
    public void ForDay_on_the_last_day_allows_everything_that_is_left()
    {
        BudgetWeather.ForDay(3000m, 2900m, 0m, new DateOnly(2026, 9, 30))
            .DailyAllowance.Should().Be(100m);
    }

    [Fact]
    public void ForecastMonth_projects_the_current_pace_to_month_end()
    {
        // 1500 in 10 days = 150/day, 4500 over 30 days: 1.5x the budget is still "rainy", not a storm.
        var forecast = BudgetWeather.ForecastMonth(3000m, 1500m, new DateOnly(2026, 9, 10));

        forecast.ProjectedSpend.Should().Be(4500m);
        forecast.Difference.Should().Be(-1500m);
        forecast.Condition.Should().Be(WeatherCondition.Rainy);
    }

    [Fact]
    public void ForecastMonth_reports_leftover_as_a_positive_difference()
    {
        var forecast = BudgetWeather.ForecastMonth(3000m, 300m, new DateOnly(2026, 9, 10));

        forecast.ProjectedSpend.Should().Be(900m);
        forecast.Difference.Should().Be(2100m);
        forecast.Condition.Should().Be(WeatherCondition.Sunny);
    }

    [Fact]
    public void ForDay_rejects_a_non_positive_budget()
    {
        var act = () => BudgetWeather.ForDay(0m, 0m, 0m, FirstOfSeptember);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
