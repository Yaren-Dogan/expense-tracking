namespace ExpenseTracker.Application.Features.Tasks;

public sealed class TaskOptions
{
    public const string SectionName = "Tasks";

    /// <summary>The assignee is reminded once when the due date is this many days away or closer.</summary>
    public int ReminderDaysBefore { get; set; } = 3;

    /// <summary>How often the background job looks for tasks that are due soon or overdue.</summary>
    public int ReminderIntervalMinutes { get; set; } = 60;
}
