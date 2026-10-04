using ExpenseTracker.Domain.Common;
using ExpenseTracker.Domain.ValueObjects;

namespace ExpenseTracker.Domain.Entities;

/// <summary>The spending limit a user sets for one calendar month.</summary>
public sealed class Budget : Entity
{
    public const decimal MaxAmount = 10_000_000m;

    /// <summary>The personal or household ledger this budget applies to.</summary>
    public Guid LedgerId { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }
    public decimal Amount { get; private set; }

    public BudgetMonth Period => new(Year, Month);

    private Budget() { }

    public static Budget Create(Guid ledgerId, BudgetMonth period, decimal amount)
    {
        if (ledgerId == Guid.Empty)
            throw new DomainException("A budget must belong to a ledger.");

        var budget = new Budget { LedgerId = ledgerId, Year = period.Year, Month = period.Month };
        budget.ChangeAmount(amount);
        return budget;
    }

    public void MoveToLedger(Guid ledgerId)
    {
        if (ledgerId == Guid.Empty)
            throw new DomainException("A budget must belong to a ledger.");
        LedgerId = ledgerId;
    }

    public void ChangeAmount(decimal amount)
    {
        if (amount <= 0 || amount > MaxAmount)
            throw new DomainException($"Budget must be greater than 0 and at most {MaxAmount}.");
        if (decimal.Round(amount, 2) != amount)
            throw new DomainException("Budget can have at most two decimal places.");

        Amount = amount;
    }
}
