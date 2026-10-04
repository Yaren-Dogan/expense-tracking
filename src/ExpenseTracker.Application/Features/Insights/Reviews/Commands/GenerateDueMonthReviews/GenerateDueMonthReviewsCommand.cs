using ExpenseTracker.Application.Common.Exceptions;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Common.Logging;
using ExpenseTracker.Application.Features.Insights.Reviews.Commands.GenerateMonthReview;
using ExpenseTracker.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExpenseTracker.Application.Features.Insights.Reviews.Commands.GenerateDueMonthReviews;

/// <summary>
/// The monthly job: reviews last month for every ledger that spent something in it and has no review yet, a batch at a
/// time (model quotas), and notifies the members. Safe to run again; reviewed ledgers are skipped.
/// </summary>
/// <returns>How many reviews were written.</returns>
public sealed record GenerateDueMonthReviewsCommand : IRequest<int>;

public sealed class GenerateDueMonthReviewsCommandHandler(
    IAppDbContext db,
    IAppClock clock,
    IUserDirectory users,
    ISender mediator,
    IOptions<InsightOptions> options,
    ILogger<GenerateDueMonthReviewsCommandHandler> logger)
    : IRequestHandler<GenerateDueMonthReviewsCommand, int>
{
    public async Task<int> Handle(GenerateDueMonthReviewsCommand request, CancellationToken cancellationToken)
    {
        var month = BudgetMonth.From(BudgetMonth.From(clock.Today).FirstDay.AddMonths(-1));

        // A background job spans every ledger and has no signed-in user: filters are bypassed on purpose.
        var ledgers = await db.Expenses.IgnoreQueryFilters()
            .Where(e => e.SpentOn >= month.FirstDay && e.SpentOn <= month.LastDay)
            .Select(e => e.LedgerId)
            .Distinct()
            .Where(l => !db.MonthReviews.IgnoreQueryFilters().Any(r => r.LedgerId == l && r.Year == month.Year && r.Month == month.Month))
            .OrderBy(l => l)
            .ToListAsync(cancellationToken);

        var written = 0;
        foreach (var ledgerId in ledgers)
        {
            if (written >= options.Value.ReviewBatchSize)
                break;

            // Data recorded under a device id before accounts existed has nobody to read it. Such ledgers are skipped
            // without counting against the batch, so they can never crowd out real households.
            if ((await LedgerMembers.ResolveAsync(db, users, ledgerId, cancellationToken)).Count == 0)
                continue;

            try
            {
                if (await mediator.Send(new GenerateMonthReviewCommand(ledgerId, month, Notify: true), cancellationToken) is not null)
                    written++;
            }
            catch (ConflictException)
            {
                // A member asked for this review at the same moment; theirs stands. Carry on with the others.
            }
        }

        logger.DueMonthReviewsGenerated(written, month.ToString());
        return written;
    }
}
