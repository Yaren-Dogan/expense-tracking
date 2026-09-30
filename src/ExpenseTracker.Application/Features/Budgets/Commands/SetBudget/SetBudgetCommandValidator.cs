using ExpenseTracker.Domain.Entities;
using FluentValidation;

namespace ExpenseTracker.Application.Features.Budgets.Commands.SetBudget;

public sealed class SetBudgetCommandValidator : AbstractValidator<SetBudgetCommand>
{
    public SetBudgetCommandValidator()
    {
        RuleFor(x => x.Month).ValidMonth();
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .LessThanOrEqualTo(Budget.MaxAmount)
            .PrecisionScale(10, 2, ignoreTrailingZeros: true);
    }
}
