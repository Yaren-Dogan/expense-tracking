using ExpenseTracker.Domain.Common;

namespace ExpenseTracker.Domain.Notifications;

/// <summary>
/// An in-app message for one user. The text is written when the event happens, so it still reads correctly after its
/// subject (a task, a month review, a budget plan) changes or is deleted. At most one notification of each kind per user
/// and subject (unique index), which keeps background jobs from repeating themselves.
/// </summary>
public sealed class Notification : Entity
{
    public const int MessageMaxLength = 200;

    public Guid UserId { get; private set; }
    public NotificationKind Kind { get; private set; }
    /// <summary>What the notification is about: a task, a month review or a budget plan, depending on <see cref="Kind"/>.</summary>
    public Guid SubjectId { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public DateTimeOffset? ReadAt { get; private set; }

    private Notification() { }

    public static Notification Create(Guid userId, NotificationKind kind, Guid subjectId, string message)
    {
        if (userId == Guid.Empty || subjectId == Guid.Empty)
            throw new DomainException("A notification needs a recipient and a subject.");
        if (!Enum.IsDefined(kind))
            throw new DomainException("Unknown notification kind.");
        if (string.IsNullOrWhiteSpace(message))
            throw new DomainException("A notification needs a message.");

        var text = message.Trim();
        return new Notification
        {
            UserId = userId,
            Kind = kind,
            SubjectId = subjectId,
            // A long task title must not make the notification unsaveable.
            Message = text.Length <= MessageMaxLength ? text : text[..(MessageMaxLength - 1)] + "…"
        };
    }

    public void MarkRead(DateTimeOffset now) => ReadAt ??= now;
}
