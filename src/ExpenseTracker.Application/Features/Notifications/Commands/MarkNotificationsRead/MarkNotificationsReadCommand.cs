using ExpenseTracker.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Features.Notifications.Commands.MarkNotificationsRead;

/// <summary>Opening the bell marks everything in it as read.</summary>
public sealed record MarkNotificationsReadCommand : IRequest;

public sealed class MarkNotificationsReadCommandHandler(IAppDbContext db, IUserContext user, TimeProvider time)
    : IRequestHandler<MarkNotificationsReadCommand>
{
    public async Task Handle(MarkNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        var userId = user.UserId;
        var unread = await db.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ToListAsync(cancellationToken);
        if (unread.Count == 0)
            return;

        var now = time.GetUtcNow();
        foreach (var notification in unread)
            notification.MarkRead(now);
        await db.SaveChangesAsync(cancellationToken);
    }
}
