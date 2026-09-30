using ExpenseTracker.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace ExpenseTracker.Infrastructure.Time;

public sealed class AppClock(TimeProvider timeProvider, IOptions<AppClockOptions> options) : IAppClock
{
    private readonly TimeZoneInfo _zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), _zone).DateTime);
}
