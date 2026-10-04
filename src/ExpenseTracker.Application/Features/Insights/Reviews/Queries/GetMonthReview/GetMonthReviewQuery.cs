using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Features.Insights.Reviews.Queries.GetMonthReview;

/// <returns>Null when the month has not been reviewed yet.</returns>
public sealed record GetMonthReviewQuery(string Month) : IRequest<MonthReviewDto?>;

public sealed class GetMonthReviewQueryValidator : AbstractValidator<GetMonthReviewQuery>
{
    public GetMonthReviewQueryValidator() =>
        RuleFor(x => x.Month).Must(m => BudgetMonth.TryParse(m, out _)).WithMessage("Ay yyyy-MM formatında olmalı.");
}

public sealed class GetMonthReviewQueryHandler(IAppDbContext db, IUserContext user)
    : IRequestHandler<GetMonthReviewQuery, MonthReviewDto?>
{
    public async Task<MonthReviewDto?> Handle(GetMonthReviewQuery request, CancellationToken cancellationToken)
    {
        BudgetMonth.TryParse(request.Month, out var month);
        var ledgerId = user.LedgerId;
        var review = await db.MonthReviews.AsNoTracking()
            .SingleOrDefaultAsync(r => r.LedgerId == ledgerId && r.Year == month.Year && r.Month == month.Month, cancellationToken);
        return review is null ? null : MonthReviewDto.From(review);
    }
}
