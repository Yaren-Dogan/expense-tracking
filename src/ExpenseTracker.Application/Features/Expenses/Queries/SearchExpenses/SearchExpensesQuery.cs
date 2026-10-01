using ExpenseTracker.Application.Common.Interfaces;
using MediatR;

namespace ExpenseTracker.Application.Features.Expenses.Queries.SearchExpenses;

/// <summary>Meaning-based search over the current user's expenses ("kahve" also finds "Starbucks latte").</summary>
public sealed record SearchExpensesQuery(string Query, int Top = 10) : IRequest<IReadOnlyList<ExpenseMatch>>;
