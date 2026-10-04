using ExpenseTracker.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Features.Notifications.Queries.ListNotifications;

/// <summary>The bell's contents: the latest notifications and how many are unread.</summary>
public sealed record ListNotificationsQuery : IRequest<NotificationListDto>;

public sealed class ListNotificationsQueryHandler(IAppDbContext db, IUserContext user)
    : IRequestHandler<ListNotificationsQuery, NotificationListDto>
{
    private const int Limit = 30;

    public async Task<NotificationListDto> Handle(ListNotificationsQuery request, CancellationToken cancellationToken)
    {
        var userId = user.UserId;
        var mine = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);

        var unread = await mine.CountAsync(n => n.ReadAt == null, cancellationToken);
        var items = await mine
            .OrderByDescending(n => n.CreatedAt)
            .Take(Limit)
            .Select(n => new NotificationDto(n.Id, n.Kind, n.Message, n.TaskId, n.CreatedAt, n.ReadAt != null))
            .ToListAsync(cancellationToken);

        return new NotificationListDto(unread, items);
    }
}
