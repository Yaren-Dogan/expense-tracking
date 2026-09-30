using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Features.Expenses.Queries.ListExpenses;

public sealed class ListExpensesQueryHandler(IAppDbContext db, IUserContext user, IAppClock clock)
    : IRequestHandler<ListExpensesQuery, IReadOnlyList<ExpenseDto>>
{
    public async Task<IReadOnlyList<ExpenseDto>> Handle(ListExpensesQuery request, CancellationToken cancellationToken)
    {
        var month = BudgetMonth.TryParse(request.Month, out var parsed) ? parsed : BudgetMonth.From(clock.Today);
        var (from, to, userId) = (month.FirstDay, month.LastDay, user.UserId);

        return await db.Expenses
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.SpentOn >= from && e.SpentOn <= to)
            .OrderByDescending(e => e.SpentOn)
            .ThenByDescending(e => e.CreatedAt)
            .Select(e => new ExpenseDto(e.Id, e.Amount, e.Category, e.Note, e.SpentOn, e.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
