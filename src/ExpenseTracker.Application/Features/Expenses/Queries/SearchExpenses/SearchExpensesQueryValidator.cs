using FluentValidation;

namespace ExpenseTracker.Application.Features.Expenses.Queries.SearchExpenses;

public sealed class SearchExpensesQueryValidator : AbstractValidator<SearchExpensesQuery>
{
    public SearchExpensesQueryValidator()
    {
        RuleFor(x => x.Query).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Top).InclusiveBetween(1, 50);
    }
}
