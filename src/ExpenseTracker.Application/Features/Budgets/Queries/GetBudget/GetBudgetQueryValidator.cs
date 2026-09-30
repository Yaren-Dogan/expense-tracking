using FluentValidation;

namespace ExpenseTracker.Application.Features.Budgets.Queries.GetBudget;

public sealed class GetBudgetQueryValidator : AbstractValidator<GetBudgetQuery>
{
    public GetBudgetQueryValidator() => RuleFor(x => x.Month).ValidMonth();
}
