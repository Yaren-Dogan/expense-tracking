using MediatR;

namespace ExpenseTracker.Application.Features.Expenses.Queries.ListExpenses;

/// <param name="Month"><c>yyyy-MM</c>; the current month when omitted.</param>
public sealed record ListExpensesQuery(string? Month) : IRequest<IReadOnlyList<ExpenseDto>>;
