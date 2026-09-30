using ExpenseTracker.Domain.ValueObjects;
using FluentValidation;

namespace ExpenseTracker.Application.Features.Expenses.Queries.ListExpenses;

public sealed class ListExpensesQueryValidator : AbstractValidator<ListExpensesQuery>
{
    public ListExpensesQueryValidator()
    {
        RuleFor(x => x.Month)
            .Must(m => BudgetMonth.TryParse(m, out _))
            .When(x => x.Month is not null)
            .WithMessage("Month must be in yyyy-MM format.");
    }
}
