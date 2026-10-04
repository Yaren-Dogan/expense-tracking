namespace ExpenseTracker.Domain.Insights;

/// <summary>How hard the planner trims a category. The percentages live in code, not in the model's answer.</summary>
public enum CutLevel
{
    Light,
    Medium,
    Strong
}
