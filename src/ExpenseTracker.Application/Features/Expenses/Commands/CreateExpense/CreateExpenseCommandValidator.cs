using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain.Entities;
using FluentValidation;

namespace ExpenseTracker.Application.Features.Expenses.Commands.CreateExpense;

public sealed class CreateExpenseCommandValidator : AbstractValidator<CreateExpenseCommand>
{
    public static readonly DateOnly EarliestDate = new(2000, 1, 1);

    public CreateExpenseCommandValidator(IAppClock clock)
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .LessThanOrEqualTo(Expense.MaxAmount)
            .PrecisionScale(9, 2, ignoreTrailingZeros: true);

        RuleFor(x => x.Category).IsInEnum();

        RuleFor(x => x.Note).MaximumLength(Expense.NoteMaxLength);

        // One day of slack: the client sends its local date, which can be ahead of the server's zone.
        RuleFor(x => x.SpentOn)
            .GreaterThanOrEqualTo(EarliestDate)
            .Must(date => date <= clock.Today.AddDays(1))
            .WithMessage("Spent date cannot be in the future.");

        RuleFor(x => x.IdempotencyKey).MaximumLength(Expense.IdempotencyKeyMaxLength);
    }
}
