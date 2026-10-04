using ExpenseTracker.Application.Common.Exceptions;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Features.Insights.Reviews.Commands.GenerateMonthReview;
using ExpenseTracker.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Features.Insights.Reviews.Commands.CreateMonthReview;

/// <summary>The signed-in user asks for a finished month's review, or for a fresh one with <paramref name="Refresh"/>.</summary>
public sealed record CreateMonthReviewCommand(string Month, bool Refresh) : IRequest<MonthReviewDto>;

public sealed class CreateMonthReviewCommandValidator : AbstractValidator<CreateMonthReviewCommand>
{
    public CreateMonthReviewCommandValidator(IAppClock clock) =>
        RuleFor(x => x.Month)
            .Must(m => BudgetMonth.TryParse(m, out var month) && month.LastDay < clock.Today)
            .WithMessage("Değerlendirme sadece bitmiş aylar için yapılır (yyyy-MM).");
}

public sealed class CreateMonthReviewCommandHandler(IAppDbContext db, IUserContext user, ISender mediator)
    : IRequestHandler<CreateMonthReviewCommand, MonthReviewDto>
{
    public async Task<MonthReviewDto> Handle(CreateMonthReviewCommand request, CancellationToken cancellationToken)
    {
        BudgetMonth.TryParse(request.Month, out var month);
        var ledgerId = user.LedgerId;

        if (!request.Refresh)
        {
            var existing = await db.MonthReviews.AsNoTracking()
                .SingleOrDefaultAsync(r => r.LedgerId == ledgerId && r.Year == month.Year && r.Month == month.Month, cancellationToken);
            if (existing is not null)
                return MonthReviewDto.From(existing);
        }

        return await mediator.Send(new GenerateMonthReviewCommand(ledgerId, month, Notify: false), cancellationToken)
            ?? throw new NotFoundException("Bu ayda harcama yok, değerlendirilecek bir şey bulunamadı.");
    }
}
