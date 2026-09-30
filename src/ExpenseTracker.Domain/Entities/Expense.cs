using ExpenseTracker.Domain.Common;
using ExpenseTracker.Domain.Enums;

namespace ExpenseTracker.Domain.Entities;

public sealed class Expense : Entity
{
    public const decimal MaxAmount = 1_000_000m;
    public const int NoteMaxLength = 200;
    public const int IdempotencyKeyMaxLength = 100;

    public Guid UserId { get; private set; }
    public decimal Amount { get; private set; }
    public ExpenseCategory Category { get; private set; }
    public string? Note { get; private set; }
    public DateOnly SpentOn { get; private set; }
    public string? IdempotencyKey { get; private set; }

    private Expense() { }

    public static Expense Create(
        Guid userId,
        decimal amount,
        ExpenseCategory category,
        string? note,
        DateOnly spentOn,
        string? idempotencyKey)
    {
        if (userId == Guid.Empty)
            throw new DomainException("An expense must belong to a user.");
        if (amount <= 0 || amount > MaxAmount)
            throw new DomainException($"Amount must be greater than 0 and at most {MaxAmount}.");
        if (decimal.Round(amount, 2) != amount)
            throw new DomainException("Amount can have at most two decimal places.");
        if (!Enum.IsDefined(category))
            throw new DomainException("Unknown category.");

        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmedNote?.Length > NoteMaxLength)
            throw new DomainException($"Note can be at most {NoteMaxLength} characters.");
        if (idempotencyKey?.Length > IdempotencyKeyMaxLength)
            throw new DomainException($"Idempotency key can be at most {IdempotencyKeyMaxLength} characters.");

        return new Expense
        {
            UserId = userId,
            Amount = amount,
            Category = category,
            Note = trimmedNote,
            SpentOn = spentOn,
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey
        };
    }
}
