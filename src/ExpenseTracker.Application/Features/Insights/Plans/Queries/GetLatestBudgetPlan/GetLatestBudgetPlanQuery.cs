using ExpenseTracker.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Features.Insights.Plans.Queries.GetLatestBudgetPlan;

/// <returns>The ledger's most recent plan, or null when none was made.</returns>
public sealed record GetLatestBudgetPlanQuery : IRequest<BudgetPlanDto?>;

public sealed class GetLatestBudgetPlanQueryHandler(IAppDbContext db, IUserContext user, IUserDirectory users)
    : IRequestHandler<GetLatestBudgetPlanQuery, BudgetPlanDto?>
{
    public async Task<BudgetPlanDto?> Handle(GetLatestBudgetPlanQuery request, CancellationToken cancellationToken)
    {
        var ledgerId = user.LedgerId;
        var plan = await db.BudgetPlans.AsNoTracking()
            .Where(p => p.LedgerId == ledgerId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return plan is null ? null : await BudgetPlanDto.FromAsync(plan, users, cancellationToken);
    }
}
