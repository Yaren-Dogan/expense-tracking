using ExpenseTracker.Domain.Insights;

namespace ExpenseTracker.Application.Features.Insights.Reviews;

/// <param name="GeneratedBy">The model that wrote it, or "kurallar" when the rules did (no model, or its answer was unusable).</param>
public sealed record MonthReviewDto(
    Guid Id,
    string Month,
    string Headline,
    IReadOnlyList<string> Highlights,
    string Tip,
    string GeneratedBy,
    DateTimeOffset GeneratedAt)
{
    public static MonthReviewDto From(MonthReview r) =>
        new(r.Id, r.Period.ToString(), r.Headline, r.Highlights, r.Tip, r.GeneratedBy, r.UpdatedAt ?? r.CreatedAt);
}
