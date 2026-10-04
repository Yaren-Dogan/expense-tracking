using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Features.Expenses.Queries.ListExpenses;

public sealed class ListExpensesQueryHandler(IAppDbContext db, IUserContext user, IAppClock clock, IUserDirectory users)
    : IRequestHandler<ListExpensesQuery, IReadOnlyList<ExpenseDto>>
{
    public async Task<IReadOnlyList<ExpenseDto>> Handle(ListExpensesQuery request, CancellationToken cancellationToken)
    {
        var month = BudgetMonth.TryParse(request.Month, out var parsed) ? parsed : BudgetMonth.From(clock.Today);
        var (from, to, ledgerId) = (month.FirstDay, month.LastDay, user.LedgerId);

        var expenses = await db.Expenses
            .AsNoTracking()
            .Where(e => e.LedgerId == ledgerId && e.SpentOn >= from && e.SpentOn <= to)
            .OrderByDescending(e => e.SpentOn)
            .ThenByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        var names = await users.GetDisplayNamesAsync(expenses.Select(e => e.UserId).Distinct(), cancellationToken);
        return expenses.Select(e => ExpenseDto.From(e, names.GetValueOrDefault(e.UserId))).ToList();
    }
}
