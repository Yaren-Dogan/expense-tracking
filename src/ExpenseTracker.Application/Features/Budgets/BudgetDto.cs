using ExpenseTracker.Domain.Entities;

namespace ExpenseTracker.Application.Features.Budgets;

public sealed record BudgetDto(string Month, decimal Amount)
{
    public static BudgetDto From(Budget b) => new(b.Period.ToString(), b.Amount);
}
