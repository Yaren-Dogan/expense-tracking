namespace ExpenseTracker.Domain.Weather;

/// <summary>Weather for a single day, relative to that day's fair share of the remaining budget.</summary>
/// <param name="Ratio">Spent on the day divided by the daily allowance; null when there is no allowance left.</param>
public sealed record DayWeather(
    WeatherCondition Condition,
    decimal DailyAllowance,
    decimal SpentOnDay,
    decimal SafeToSpend,
    decimal? Ratio);

/// <param name="Difference">Budget minus projected spend: positive is left over, negative is overspend.</param>
public sealed record MonthForecast(
    WeatherCondition Condition,
    decimal ProjectedSpend,
    decimal Difference);

/// <summary>
/// Turns budget numbers into weather. Pure functions: every rule of docs/prd.md "Hava durumu kuralları" lives here.
/// </summary>
public static class BudgetWeather
{
    public const decimal SunnyMax = 0.5m;
    public const decimal PartlyCloudyMax = 0.9m;
    public const decimal CloudyMax = 1.0m;
    public const decimal RainyMax = 1.5m;

    public static WeatherCondition Classify(decimal ratio) => ratio switch
    {
        <= SunnyMax => WeatherCondition.Sunny,
        <= PartlyCloudyMax => WeatherCondition.PartlyCloudy,
        <= CloudyMax => WeatherCondition.Cloudy,
        <= RainyMax => WeatherCondition.Rainy,
        _ => WeatherCondition.Stormy
    };

    public static DayWeather ForDay(decimal monthlyBudget, decimal spentBeforeDay, decimal spentOnDay, DateOnly day)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(monthlyBudget);
        ArgumentOutOfRangeException.ThrowIfNegative(spentBeforeDay);
        ArgumentOutOfRangeException.ThrowIfNegative(spentOnDay);

        var remainingBudget = monthlyBudget - spentBeforeDay;
        if (remainingBudget <= 0)
        {
            // The month's budget is already gone: any spending is a storm, holding back is merely cloudy.
            var condition = spentOnDay > 0 ? WeatherCondition.Stormy : WeatherCondition.Cloudy;
            return new DayWeather(condition, 0m, spentOnDay, 0m, null);
        }

        var remainingDays = DateTime.DaysInMonth(day.Year, day.Month) - day.Day + 1;
        var allowance = remainingBudget / remainingDays;
        var ratio = spentOnDay / allowance;

        return new DayWeather(
            Classify(ratio),
            RoundMoney(allowance),
            spentOnDay,
            RoundMoney(Math.Max(0m, allowance - spentOnDay)),
            decimal.Round(ratio, 2, MidpointRounding.AwayFromZero));
    }

    public static MonthForecast ForecastMonth(decimal monthlyBudget, decimal spentThroughDay, DateOnly day)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(monthlyBudget);
        ArgumentOutOfRangeException.ThrowIfNegative(spentThroughDay);

        var daysInMonth = DateTime.DaysInMonth(day.Year, day.Month);
        var projected = RoundMoney(spentThroughDay / day.Day * daysInMonth);

        return new MonthForecast(Classify(projected / monthlyBudget), projected, monthlyBudget - projected);
    }

    private static decimal RoundMoney(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
