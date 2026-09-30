using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Features.Budgets.Queries.GetBudget;

public sealed class GetBudgetQueryHandler(IAppDbContext db, IUserContext user)
    : IRequestHandler<GetBudgetQuery, BudgetDto?>
{
    public async Task<BudgetDto?> Handle(GetBudgetQuery request, CancellationToken cancellationToken)
    {
        BudgetMonth.TryParse(request.Month, out var month);
        var userId = user.UserId;

        var budget = await db.Budgets
            .AsNoTracking()
            .SingleOrDefaultAsync(b => b.UserId == userId && b.Year == month.Year && b.Month == month.Month, cancellationToken);

        return budget is null ? null : BudgetDto.From(budget);
    }
}
