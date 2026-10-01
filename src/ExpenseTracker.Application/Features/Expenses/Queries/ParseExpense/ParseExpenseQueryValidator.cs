using ExpenseTracker.Application.Common.Interfaces;
using FluentValidation;

namespace ExpenseTracker.Application.Features.Expenses.Queries.ParseExpense;

public sealed class ParseExpenseQueryValidator : AbstractValidator<ParseExpenseQuery>
{
    public const int MaxTextLength = 300;

    public ParseExpenseQueryValidator(IAppClock clock)
    {
        RuleFor(x => x.Text).NotEmpty().MaximumLength(MaxTextLength);
        RuleFor(x => x.Today!.Value)
            .Must(d => d <= clock.Today.AddDays(1) && d >= clock.Today.AddDays(-1))
            .WithMessage("Today must be the client's current date.")
            .OverridePropertyName(nameof(ParseExpenseQuery.Today))
            .When(x => x.Today is not null);
    }
}
