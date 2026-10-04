using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Common.Logging;
using ExpenseTracker.Domain.ValueObjects;
using ExpenseTracker.Domain.Weather;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Application.Features.Weather.Queries.GetWeather;

public sealed class GetWeatherQueryHandler(
    IAppDbContext db,
    IUserContext user,
    IAppClock clock,
    ILogger<GetWeatherQueryHandler> logger)
    : IRequestHandler<GetWeatherQuery, WeatherDto>
{
    private const int StripDays = 7;

    public async Task<WeatherDto> Handle(GetWeatherQuery request, CancellationToken cancellationToken)
    {
        var ledgerId = user.LedgerId;
        var today = request.Date ?? clock.Today;
        var month = BudgetMonth.From(today);
        var stripStart = today.AddDays(-(StripDays - 1));

        // Each strip day needs everything spent since the start of *its* month, so load from the
        // first day of the strip's oldest month.
        var from = BudgetMonth.From(stripStart).FirstDay;
        var dailyTotals = await db.Expenses
            .AsNoTracking()
            .Where(e => e.LedgerId == ledgerId && e.SpentOn >= from && e.SpentOn <= today)
            .GroupBy(e => e.SpentOn)
            .Select(g => new { Day = g.Key, Total = g.Sum(e => e.Amount) })
            .ToDictionaryAsync(x => x.Day, x => x.Total, cancellationToken);

        var budgets = await LoadBudgets(ledgerId, BudgetMonth.From(stripStart), month, cancellationToken);

        decimal SpentOn(DateOnly day) => dailyTotals.GetValueOrDefault(day);
        decimal SpentBetween(DateOnly start, DateOnly endExclusive) =>
            dailyTotals.Where(kv => kv.Key >= start && kv.Key < endExclusive).Sum(kv => kv.Value);

        DayWeather? WeatherFor(DateOnly day)
        {
            var dayMonth = BudgetMonth.From(day);
            return budgets.TryGetValue(dayMonth, out var budget)
                ? BudgetWeather.ForDay(budget, SpentBetween(dayMonth.FirstDay, day), SpentOn(day), day)
                : null;
        }

        var strip = Enumerable.Range(0, StripDays)
            .Select(i => stripStart.AddDays(i))
            .Select(day => new DayWeatherDto(day, WeatherFor(day)?.Condition, SpentOn(day)))
            .ToList();

        var spentThisMonth = SpentBetween(month.FirstDay, today.AddDays(1));
        var todayWeather = WeatherFor(today);
        decimal? monthlyBudget = budgets.TryGetValue(month, out var b) ? b : null;
        var forecast = monthlyBudget is { } mb ? BudgetWeather.ForecastMonth(mb, spentThisMonth, today) : null;

        logger.WeatherCalculated(ledgerId, today, todayWeather?.Condition);

        return new WeatherDto(
            today,
            month.ToString(),
            monthlyBudget,
            spentThisMonth,
            monthlyBudget - spentThisMonth,
            todayWeather is null
                ? null
                : new TodayWeatherDto(todayWeather.Condition, todayWeather.DailyAllowance, todayWeather.SpentOnDay,
                    todayWeather.SafeToSpend, todayWeather.Ratio),
            strip,
            forecast is null ? null : new MonthForecastDto(forecast.Condition, forecast.ProjectedSpend, forecast.Difference));
    }

    private async Task<Dictionary<BudgetMonth, decimal>> LoadBudgets(
        Guid ledgerId, BudgetMonth first, BudgetMonth last, CancellationToken ct)
    {
        var rows = await db.Budgets
            .AsNoTracking()
            .Where(b => b.LedgerId == ledgerId
                && ((b.Year == first.Year && b.Month == first.Month) || (b.Year == last.Year && b.Month == last.Month)))
            .Select(b => new { b.Year, b.Month, b.Amount })
            .ToListAsync(ct);

        return rows.ToDictionary(r => new BudgetMonth(r.Year, r.Month), r => r.Amount);
    }
}
