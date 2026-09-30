using ExpenseTracker.Domain.Entities;
using ExpenseTracker.Domain.Enums;

namespace ExpenseTracker.Application.Features.Expenses;

public sealed record ExpenseDto(
    Guid Id,
    decimal Amount,
    ExpenseCategory Category,
    string? Note,
    DateOnly SpentOn,
    DateTimeOffset CreatedAt)
{
    public static ExpenseDto From(Expense e) => new(e.Id, e.Amount, e.Category, e.Note, e.SpentOn, e.CreatedAt);
}
