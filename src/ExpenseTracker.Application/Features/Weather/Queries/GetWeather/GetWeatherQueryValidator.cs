using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain.ValueObjects;
using FluentValidation;

namespace ExpenseTracker.Application.Features.Weather.Queries.GetWeather;

public sealed class GetWeatherQueryValidator : AbstractValidator<GetWeatherQuery>
{
    public GetWeatherQueryValidator(IAppClock clock)
    {
        RuleFor(x => x.Date!.Value)
            .GreaterThanOrEqualTo(new DateOnly(BudgetMonth.MinYear, 1, 1))
            .Must(date => date <= clock.Today.AddDays(1))
            .WithMessage("Date cannot be in the future.")
            .OverridePropertyName(nameof(GetWeatherQuery.Date))
            .When(x => x.Date is not null);
    }
}
