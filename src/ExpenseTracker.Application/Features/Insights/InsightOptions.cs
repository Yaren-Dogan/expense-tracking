namespace ExpenseTracker.Application.Features.Insights;

public sealed class InsightOptions
{
    public const string SectionName = "Insights";

    /// <summary>How often the background job looks for finished months without a review.</summary>
    public int ReviewIntervalMinutes { get; set; } = 60;

    /// <summary>Reviews written per run, so a busy month start does not burn the model quota at once.</summary>
    public int ReviewBatchSize { get; set; } = 20;
}
