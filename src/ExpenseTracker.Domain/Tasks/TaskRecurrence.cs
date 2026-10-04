namespace ExpenseTracker.Domain.Tasks;

public enum TaskRecurrence
{
    None,

    /// <summary>Completing the task opens next month's, on the same day of the month (clamped to short months).</summary>
    Monthly
}
