using ExpenseTracker.Domain.Notifications;

namespace ExpenseTracker.Application.Features.Notifications;

public sealed record NotificationDto(Guid Id, NotificationKind Kind, string Message, Guid SubjectId, DateTimeOffset CreatedAt, bool IsRead);

public sealed record NotificationListDto(int UnreadCount, IReadOnlyList<NotificationDto> Items);
