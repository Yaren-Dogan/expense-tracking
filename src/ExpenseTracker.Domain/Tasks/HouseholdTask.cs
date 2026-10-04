using ExpenseTracker.Domain.Common;
using ExpenseTracker.Domain.Entities;
using ExpenseTracker.Domain.Enums;

namespace ExpenseTracker.Domain.Tasks;

/// <summary>
/// A job given to someone in a ledger, typically paying a bill ("Elektrik faturası → Yaren"). It lives in the same ledger
/// as expenses, so a household's tasks are shared and a user without a household can keep personal ones.
/// </summary>
public sealed class HouseholdTask : Entity
{
    public const int TitleMaxLength = 80;

    public Guid LedgerId { get; private set; }

    /// <summary>All occurrences of a monthly task share it; (SeriesId, DueOn) is unique, so one month cannot open twice.</summary>
    public Guid SeriesId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    /// <summary>The category of the expense recorded when the task is paid.</summary>
    public ExpenseCategory Category { get; private set; }

    public Guid AssigneeId { get; private set; }
    public Guid CreatedById { get; private set; }
    public DateOnly DueOn { get; private set; }
    public TaskRecurrence Recurrence { get; private set; }

    /// <summary>The day of month the series was created for; a 31st stays the 31st after passing through a 30-day month.</summary>
    public int AnchorDay { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }
    public Guid? CompletedById { get; private set; }

    /// <summary>The expense recorded when the task was marked paid with an amount.</summary>
    public Guid? ExpenseId { get; private set; }
    public decimal? PaidAmount { get; private set; }

    public bool IsDone => CompletedAt is not null;

    private HouseholdTask() { }

    public static HouseholdTask Create(
        Guid ledgerId,
        string title,
        ExpenseCategory category,
        Guid assigneeId,
        Guid createdById,
        DateOnly dueOn,
        TaskRecurrence recurrence)
    {
        if (ledgerId == Guid.Empty || assigneeId == Guid.Empty || createdById == Guid.Empty)
            throw new DomainException("A task needs a ledger, an assignee and a creator.");
        if (!Enum.IsDefined(category))
            throw new DomainException("Unknown category.");
        if (!Enum.IsDefined(recurrence))
            throw new DomainException("Unknown recurrence.");

        var task = new HouseholdTask
        {
            LedgerId = ledgerId,
            Title = ValidTitle(title),
            Category = category,
            AssigneeId = assigneeId,
            CreatedById = createdById,
            DueOn = dueOn,
            Recurrence = recurrence,
            AnchorDay = dueOn.Day
        };
        task.SeriesId = task.Id;
        return task;
    }

    /// <summary>The person doing it and the person who asked for it may tick it off.</summary>
    public bool CanBeCompletedBy(Guid userId) => userId == AssigneeId || userId == CreatedById;

    /// <summary>Marks the task done. For a monthly task, returns next month's occurrence for the caller to save.</summary>
    public HouseholdTask? Complete(Guid userId, DateTimeOffset now, Expense? payment = null)
    {
        if (IsDone)
            throw new DomainException("The task is already done.");
        if (!CanBeCompletedBy(userId))
            throw new DomainException("Only the assignee or the creator can complete the task.");
        if (payment is not null && payment.LedgerId != LedgerId)
            throw new DomainException("The payment must be recorded in the task's ledger.");

        CompletedAt = now;
        CompletedById = userId;
        ExpenseId = payment?.Id;
        PaidAmount = payment?.Amount;

        return Recurrence == TaskRecurrence.Monthly ? NextOccurrence() : null;
    }

    /// <summary>Hands an open task to someone else, e.g. when the assignee leaves the household.</summary>
    public void AssignTo(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new DomainException("A task needs an assignee.");
        if (IsDone)
            throw new DomainException("A finished task cannot be reassigned.");
        AssigneeId = userId;
    }

    private HouseholdTask NextOccurrence()
    {
        var next = DueOn.AddMonths(1);
        var day = Math.Min(AnchorDay, DateTime.DaysInMonth(next.Year, next.Month));

        return new HouseholdTask
        {
            LedgerId = LedgerId,
            SeriesId = SeriesId,
            Title = Title,
            Category = Category,
            AssigneeId = AssigneeId,
            CreatedById = CreatedById,
            DueOn = new DateOnly(next.Year, next.Month, day),
            Recurrence = Recurrence,
            AnchorDay = AnchorDay
        };
    }

    private static string ValidTitle(string title)
    {
        var trimmed = title?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > TitleMaxLength)
            throw new DomainException($"Task title must be 1-{TitleMaxLength} characters.");
        return trimmed;
    }
}
