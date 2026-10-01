using ExpenseTracker.Application.Common.Interfaces;
using MediatR;

namespace ExpenseTracker.Application.Features.Expenses.Queries.SearchExpenses;

public sealed class SearchExpensesQueryHandler(ISemanticExpenseSearch search, IUserContext user)
    : IRequestHandler<SearchExpensesQuery, IReadOnlyList<ExpenseMatch>>
{
    public Task<IReadOnlyList<ExpenseMatch>> Handle(SearchExpensesQuery request, CancellationToken cancellationToken) =>
        search.SearchAsync(user.UserId, request.Query, request.Top, cancellationToken);
}
