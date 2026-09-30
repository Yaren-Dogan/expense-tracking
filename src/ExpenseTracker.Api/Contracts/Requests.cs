using ExpenseTracker.Domain.Enums;

namespace ExpenseTracker.Api.Contracts;

// HTTP bodies are kept separate from commands so header- and route-bound values
// (idempotency key, month) cannot be spoofed through the JSON body.
public sealed record CreateExpenseRequest(decimal Amount, ExpenseCategory Category, string? Note, DateOnly SpentOn);

public sealed record SetBudgetRequest(decimal Amount);
