using ExpenseTracker.Domain.Entities;
using ExpenseTracker.Domain.Enums;

namespace ExpenseTracker.Application.Features.Expenses;

/// <param name="CreatedBy">Who entered it; in a household the list shows this and only they may delete it.</param>
public sealed record ExpenseDto(
    Guid Id,
    decimal Amount,
    ExpenseCategory Category,
    string? Note,
    DateOnly SpentOn,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    string? CreatedByName = null)
{
    public static ExpenseDto From(Expense e, string? createdByName = null) =>
        new(e.Id, e.Amount, e.Category, e.Note, e.SpentOn, e.CreatedAt, e.UserId, createdByName);
}
