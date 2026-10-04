using ExpenseTracker.Domain.Common;
using ExpenseTracker.Domain.ValueObjects;

namespace ExpenseTracker.Domain.Insights;

/// <summary>
/// The month review written by the review agent once a month is over: a headline, the facts worth noticing and a tip.
/// The facts' sentences are computed in code; the model only chooses which of them matter and writes the wording around
/// them. One review per ledger and month (unique index); regenerating replaces its content.
/// </summary>
public sealed class MonthReview : Entity
{
    public const int HeadlineMaxLength = 160;
    public const int TipMaxLength = 300;
    public const int MaxHighlights = 4;
    public const string RulesAuthor = "kurallar";

    private string[] _highlights = [];

    public Guid LedgerId { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }
    public string Headline { get; private set; } = string.Empty;
    public IReadOnlyList<string> Highlights => _highlights;
    public string Tip { get; private set; } = string.Empty;

    /// <summary>The model that chose and worded the review, or <see cref="RulesAuthor"/> when the rules did.</summary>
    public string GeneratedBy { get; private set; } = RulesAuthor;

    public BudgetMonth Period => new(Year, Month);

    private MonthReview() { }

    public static MonthReview Create(Guid ledgerId, BudgetMonth month, string headline, IEnumerable<string> highlights, string tip, string generatedBy)
    {
        if (ledgerId == Guid.Empty)
            throw new DomainException("A review must belong to a ledger.");

        var review = new MonthReview { LedgerId = ledgerId, Year = month.Year, Month = month.Month };
        review.Replace(headline, highlights, tip, generatedBy);
        return review;
    }

    /// <summary>Regenerating a review keeps its identity, so notifications that point at it stay valid.</summary>
    public void Replace(string headline, IEnumerable<string> highlights, string tip, string generatedBy)
    {
        var lines = highlights.Where(h => !string.IsNullOrWhiteSpace(h)).Select(h => h.Trim()).ToArray();
        if (string.IsNullOrWhiteSpace(headline) || lines.Length == 0)
            throw new DomainException("A review needs a headline and at least one highlight.");
        if (lines.Length > MaxHighlights)
            throw new DomainException($"A review has at most {MaxHighlights} highlights.");

        Headline = Clip(headline.Trim(), HeadlineMaxLength);
        _highlights = lines;
        Tip = Clip(tip?.Trim() ?? string.Empty, TipMaxLength);
        GeneratedBy = string.IsNullOrWhiteSpace(generatedBy) ? RulesAuthor : generatedBy.Trim();
    }

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}
