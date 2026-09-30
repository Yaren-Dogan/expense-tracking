using ExpenseTracker.Domain.ValueObjects;
using FluentValidation;

namespace ExpenseTracker.Application.Features.Budgets;

internal static class MonthRules
{
    public static IRuleBuilderOptions<T, string> ValidMonth<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .Must(m => BudgetMonth.TryParse(m, out _))
            .WithMessage("Month must be in yyyy-MM format.");
}
