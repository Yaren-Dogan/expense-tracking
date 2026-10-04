using ExpenseTracker.Application.Features.Tasks;
using ExpenseTracker.Application.Features.Tasks.Commands.SendTaskReminders;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExpenseTracker.Infrastructure.Tasks;

/// <summary>Runs the task reminders on start and then periodically; the command itself never repeats a reminder.</summary>
public sealed partial class TaskReminderScheduler(
    IServiceScopeFactory scopeFactory,
    IOptions<TaskOptions> options,
    TimeProvider timeProvider,
    ILogger<TaskReminderScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.Value.ReminderIntervalMinutes), timeProvider);

        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ISender>().Send(new SendTaskRemindersCommand(), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A database hiccup must not stop the scheduler; the next tick tries again.
                LogRemindersFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(EventId = 3101, EventName = "TaskRemindersFailed", Level = LogLevel.Warning,
        Message = "Sending task reminders failed; will retry on the next tick")]
    private static partial void LogRemindersFailed(ILogger logger, Exception exception);
}
