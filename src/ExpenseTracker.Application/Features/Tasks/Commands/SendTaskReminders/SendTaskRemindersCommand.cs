using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Common.Logging;
using ExpenseTracker.Domain.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExpenseTracker.Application.Features.Tasks.Commands.SendTaskReminders;

/// <summary>
/// Reminds assignees once when a task is due soon and once more if it becomes overdue. Run periodically by a background
/// job; running it again is harmless because each (user, task, kind) notification exists at most once.
/// </summary>
/// <returns>The number of notifications created.</returns>
public sealed record SendTaskRemindersCommand : IRequest<int>;

public sealed class SendTaskRemindersCommandHandler(
    IAppDbContext db,
    IAppClock clock,
    IOptions<TaskOptions> options,
    ILogger<SendTaskRemindersCommandHandler> logger)
    : IRequestHandler<SendTaskRemindersCommand, int>
{
    public async Task<int> Handle(SendTaskRemindersCommand request, CancellationToken cancellationToken)
    {
        var today = clock.Today;
        var horizon = today.AddDays(options.Value.ReminderDaysBefore);

        // A background job has no signed-in user and covers every ledger, so the per-user filters are bypassed on purpose.
        var tasks = await db.Tasks.IgnoreQueryFilters()
            .Where(t => t.CompletedAt == null && t.DueOn <= horizon)
            .ToListAsync(cancellationToken);
        if (tasks.Count == 0)
            return 0;

        var taskIds = tasks.Select(t => t.Id).ToList();
        var sent = (await db.Notifications.IgnoreQueryFilters()
                .Where(n => taskIds.Contains(n.TaskId) && (n.Kind == NotificationKind.TaskDueSoon || n.Kind == NotificationKind.TaskOverdue))
                .Select(n => new { n.UserId, n.TaskId, n.Kind })
                .ToListAsync(cancellationToken))
            .Select(n => (n.UserId, n.TaskId, n.Kind))
            .ToHashSet();

        var created = 0;
        foreach (var task in tasks)
        {
            var overdue = task.DueOn < today;
            var kind = overdue ? NotificationKind.TaskOverdue : NotificationKind.TaskDueSoon;
            if (sent.Contains((task.AssigneeId, task.Id, kind)))
                continue;

            var message = overdue ? TaskMessages.Overdue(task) : TaskMessages.DueSoon(task, today);
            db.Notifications.Add(Notification.Create(task.AssigneeId, kind, task.Id, message));
            created++;
        }

        if (created > 0)
            await db.SaveChangesAsync(cancellationToken);

        logger.TaskRemindersSent(created, today);
        return created;
    }
}
