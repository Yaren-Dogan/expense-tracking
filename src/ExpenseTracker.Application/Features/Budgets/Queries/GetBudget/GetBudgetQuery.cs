using MediatR;

namespace ExpenseTracker.Application.Features.Budgets.Queries.GetBudget;

public sealed record GetBudgetQuery(string Month) : IRequest<BudgetDto?>;
