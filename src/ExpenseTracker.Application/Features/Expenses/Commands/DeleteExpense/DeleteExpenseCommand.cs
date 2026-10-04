using MediatR;

namespace ExpenseTracker.Application.Features.Expenses.Commands.DeleteExpense;

/// <returns>False when the expense does not exist in the current ledger.</returns>
public sealed record DeleteExpenseCommand(Guid Id) : IRequest<bool>;
