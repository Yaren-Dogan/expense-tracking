using ExpenseTracker.Domain.Weather;
using MediatR;

namespace ExpenseTracker.Application.Features.Weather.Queries.GetWeather;

/// <param name="Date">The client's local "today"; the server's configured zone is used when omitted.</param>
public sealed record GetWeatherQuery(DateOnly? Date) : IRequest<WeatherDto>;

/// <param name="Budget">Null when the user has not set a budget for the month; weather fields are null then too.</param>
public sealed record WeatherDto(
    DateOnly Date,
    string Month,
    decimal? Budget,
    decimal SpentThisMonth,
    decimal? RemainingBudget,
    TodayWeatherDto? Today,
    IReadOnlyList<DayWeatherDto> LastSevenDays,
    MonthForecastDto? Forecast);

public sealed record TodayWeatherDto(
    WeatherCondition Condition,
    decimal DailyAllowance,
    decimal SpentToday,
    decimal SafeToSpend,
    decimal? Ratio);

/// <param name="Condition">Null when that day's month has no budget.</param>
public sealed record DayWeatherDto(DateOnly Date, WeatherCondition? Condition, decimal Spent);

public sealed record MonthForecastDto(WeatherCondition Condition, decimal ProjectedSpend, decimal Difference);
