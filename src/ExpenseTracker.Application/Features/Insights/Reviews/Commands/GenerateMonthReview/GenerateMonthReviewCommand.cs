using ExpenseTracker.Application.Common.Ai;
using ExpenseTracker.Application.Common.Exceptions;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Common.Logging;
using ExpenseTracker.Application.Common.Text;
using ExpenseTracker.Domain.Enums;
using ExpenseTracker.Domain.Insights;
using ExpenseTracker.Domain.Notifications;
using ExpenseTracker.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Application.Features.Insights.Reviews.Commands.GenerateMonthReview;

/// <summary>
/// Writes (or rewrites) the review of one ledger's month. Used by the monthly background job, which has no signed-in
/// user, and by the API on request; the ledger therefore comes from the caller, never from the model.
/// </summary>
/// <param name="Notify">Tell the ledger's members when a new review is created (the background job does).</param>
/// <returns>Null when the month has no expenses to review.</returns>
public sealed record GenerateMonthReviewCommand(Guid LedgerId, BudgetMonth Month, bool Notify) : IRequest<MonthReviewDto?>;

public sealed class GenerateMonthReviewCommandHandler(
    IAppDbContext db,
    IChatClient chatClient,
    IUserDirectory users,
    ILogger<GenerateMonthReviewCommandHandler> logger)
    : IRequestHandler<GenerateMonthReviewCommand, MonthReviewDto?>
{
    public async Task<MonthReviewDto?> Handle(GenerateMonthReviewCommand request, CancellationToken cancellationToken)
    {
        var (ledgerId, month) = (request.LedgerId, request.Month);
        var previousMonth = BudgetMonth.From(month.FirstDay.AddMonths(-1));

        // Called without a signed-in user (background job), so the ledger filters are bypassed and the ledger is
        // given explicitly in every query.
        var rows = await db.Expenses.IgnoreQueryFilters().AsNoTracking()
            .Where(e => e.LedgerId == ledgerId && e.SpentOn >= previousMonth.FirstDay && e.SpentOn <= month.LastDay)
            .Select(e => new SpendingRow(e.Id, e.Amount, e.Category, e.SpentOn, e.Note, e.UserId))
            .ToListAsync(cancellationToken);
        var expenses = rows.Where(r => r.SpentOn >= month.FirstDay).ToList();
        if (expenses.Count == 0)
            return null;

        var previous = rows.Where(r => r.SpentOn < month.FirstDay).ToList();
        var budget = await db.Budgets.IgnoreQueryFilters().AsNoTracking()
            .Where(b => b.LedgerId == ledgerId && b.Year == month.Year && b.Month == month.Month)
            .Select(b => (decimal?)b.Amount)
            .SingleOrDefaultAsync(cancellationToken);
        var names = await users.GetDisplayNamesAsync(expenses.Select(e => e.UserId).Distinct(), cancellationToken);

        var facts = MonthFacts.Compute(month, budget, expenses,
            previous.GroupBy(p => p.Category).ToDictionary(g => g.Key, g => g.Sum(p => p.Amount)), names).ToList();
        var draft = await DraftAsync(month, budget, expenses, previous, facts, cancellationToken);

        var review = await db.MonthReviews.IgnoreQueryFilters()
            .SingleOrDefaultAsync(r => r.LedgerId == ledgerId && r.Year == month.Year && r.Month == month.Month, cancellationToken);
        var isNew = review is null;
        if (review is null)
        {
            review = MonthReview.Create(ledgerId, month, draft.Headline, draft.Highlights, draft.Tip, draft.GeneratedBy);
            db.MonthReviews.Add(review);
        }
        else
        {
            review.Replace(draft.Headline, draft.Highlights, draft.Tip, draft.GeneratedBy);
        }

        if (isNew && request.Notify)
        {
            // A household member gets one review for the household and one for their personal ledger: say which is which.
            var householdName = await db.Households.AsNoTracking().Where(h => h.Id == ledgerId).Select(h => h.Name)
                .SingleOrDefaultAsync(cancellationToken);
            var message = householdName is null
                ? $"Kişisel {TurkishText.Month(month)} değerlendirmen hazır: {review.Headline}"
                : $"{householdName} · {TurkishText.Month(month)} değerlendirmesi hazır: {review.Headline}";
            foreach (var member in await LedgerMembers.ResolveAsync(db, users, ledgerId, cancellationToken))
                db.Notifications.Add(Notification.Create(member, NotificationKind.MonthReviewReady, review.Id, message));
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            // The background job and a user request wrote the same month at once; the other one's review stands.
            throw new ConflictException("Bu ayın değerlendirmesi şu an hazırlanıyor. Birazdan tekrar bak.");
        }

        logger.MonthReviewGenerated(ledgerId, month.ToString(), review.GeneratedBy);
        return MonthReviewDto.From(review);
    }

    /// <summary>The agent's review, or the rule-based one when no model is configured or it fails.</summary>
    private async Task<ReviewDraft> DraftAsync(
        BudgetMonth month, decimal? budget, List<SpendingRow> expenses, List<SpendingRow> previous, List<ReviewFact> facts,
        CancellationToken ct)
    {
        if (!AiModel.IsConfigured(chatClient))
            return ReviewAgent.Rules(month, budget, expenses.Sum(e => e.Amount), facts);

        try
        {
            var model = AiModel.Name(chatClient);
            IReadOnlyDictionary<ExpenseCategory, IReadOnlyList<SpendingRow>> byCategory =
                previous.GroupBy(p => p.Category).ToDictionary(g => g.Key, g => (IReadOnlyList<SpendingRow>)g.ToList());
            return await new ReviewAgent(chatClient, model).RunAsync(month, budget, expenses, byCategory, facts, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A review is useful even without a model: an unreachable, unconfigured or rate-limited provider must not stop it.
            logger.InsightModelUnavailable("month-review", ex.GetType().Name);
            return ReviewAgent.Rules(month, budget, expenses.Sum(e => e.Amount), facts);
        }
    }
}
