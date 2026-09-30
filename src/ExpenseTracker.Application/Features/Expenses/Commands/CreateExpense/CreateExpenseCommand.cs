using ExpenseTracker.Domain.Enums;
using MediatR;

namespace ExpenseTracker.Application.Features.Expenses.Commands.CreateExpense;

public sealed record CreateExpenseCommand(
    decimal Amount,
    ExpenseCategory Category,
    string? Note,
    DateOnly SpentOn,
    string? IdempotencyKey = null) : IRequest<CreateExpenseResult>;

/// <param name="IsReplay">True when an earlier request with the same idempotency key already created the expense.</param>
public sealed record CreateExpenseResult(ExpenseDto Expense, bool IsReplay);
