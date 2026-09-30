using MediatR;

namespace ExpenseTracker.Application.Features.Budgets.Commands.SetBudget;

/// <summary>Creates or replaces the budget for a month.</summary>
public sealed record SetBudgetCommand(string Month, decimal Amount) : IRequest<BudgetDto>;
