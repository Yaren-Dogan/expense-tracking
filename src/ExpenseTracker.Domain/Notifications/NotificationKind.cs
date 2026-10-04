namespace ExpenseTracker.Domain.Notifications;

public enum NotificationKind
{
    TaskAssigned,
    TaskDueSoon,
    TaskOverdue,
    TaskCompleted,

    /// <summary>Last month's review is ready (subject: the month review).</summary>
    MonthReviewReady,

    /// <summary>A household member applied a budget plan (subject: the plan).</summary>
    BudgetPlanApplied
}
