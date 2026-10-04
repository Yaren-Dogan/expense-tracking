using ExpenseTracker.Application.Features.Insights;
using ExpenseTracker.Application.Features.Insights.Reviews.Commands.GenerateDueMonthReviews;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExpenseTracker.Infrastructure.Insights;

/// <summary>
/// Looks for finished months without a review on start and then periodically. In practice it writes reviews in the
/// first hours of a month; every other run finds nothing to do.
/// </summary>
public sealed partial class MonthReviewScheduler(
    IServiceScopeFactory scopeFactory,
    IOptions<InsightOptions> options,
    TimeProvider timeProvider,
    ILogger<MonthReviewScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.Value.ReviewIntervalMinutes), timeProvider);

        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GenerateDueMonthReviewsCommand(), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogReviewsFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(EventId = 3102, EventName = "MonthReviewsFailed", Level = LogLevel.Warning,
        Message = "Writing month reviews failed; will retry on the next tick")]
    private static partial void LogReviewsFailed(ILogger logger, Exception exception);
}
