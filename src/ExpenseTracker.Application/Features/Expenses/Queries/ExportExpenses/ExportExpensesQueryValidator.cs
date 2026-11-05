using ExpenseTracker.Domain.ValueObjects;
using FluentValidation;

namespace ExpenseTracker.Application.Features.Expenses.Queries.ExportExpenses;

public sealed class ExportExpensesQueryValidator : AbstractValidator<ExportExpensesQuery>
{
    public ExportExpensesQueryValidator() =>
        RuleFor(x => x.Month)
            .Must(m => BudgetMonth.TryParse(m, out _))
            .WithMessage("Month must be in yyyy-MM format.");
}
