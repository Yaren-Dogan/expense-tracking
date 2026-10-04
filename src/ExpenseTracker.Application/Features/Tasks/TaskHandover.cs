using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Common.Logging;
using ExpenseTracker.Domain.Households;
using ExpenseTracker.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Application.Features.Tasks;

internal static class TaskHandover
{
    /// <summary>
    /// When someone leaves (or is removed from) a household, their open household tasks go to the owner, so no bill is
    /// left with a person who can no longer see it. Call after the member has been removed from <paramref name="household"/>;
    /// the caller saves.
    /// </summary>
    public static async Task HandOverOpenTasksAsync(
        this IAppDbContext db, Household household, Guid leaverId, IUserDirectory users, ILogger logger, CancellationToken ct)
    {
        var owner = household.Members.FirstOrDefault(m => household.IsOwner(m.UserId));
        if (owner is null)
            return; // The last member left; the household's tasks go with it.

        // The caller is a member of this household, so its id is their current ledger and the ledger filter matches.
        var tasks = await db.Tasks
            .Where(t => t.LedgerId == household.Id && t.AssigneeId == leaverId && t.CompletedAt == null)
            .ToListAsync(ct);
        if (tasks.Count == 0)
            return;

        var names = await users.GetDisplayNamesAsync([leaverId], ct);
        var leaverName = names.GetValueOrDefault(leaverId) ?? "Bir üye";
        // The notification filter is scoped to the signed-in user (the leaver or remover); these are the owner's.
        var alreadyTold = (await db.Notifications.IgnoreQueryFilters()
                .Where(n => n.UserId == owner.UserId && n.Kind == NotificationKind.TaskAssigned)
                .Select(n => n.TaskId)
                .ToListAsync(ct))
            .ToHashSet();

        foreach (var task in tasks)
        {
            task.AssignTo(owner.UserId);
            // The notification index allows one "assigned" per user and task; the owner may have held this task before.
            if (!alreadyTold.Contains(task.Id))
                db.Notifications.Add(Notification.Create(
                    owner.UserId, NotificationKind.TaskAssigned, task.Id, TaskMessages.HandedOver(leaverName, task)));
        }

        logger.TasksHandedOver(tasks.Count, leaverId, owner.UserId, household.Id);
    }
}
