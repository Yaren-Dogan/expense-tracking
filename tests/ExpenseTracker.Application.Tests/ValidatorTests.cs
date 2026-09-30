using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Features.Budgets.Commands.SetBudget;
using ExpenseTracker.Application.Features.Expenses.Commands.CreateExpense;
using ExpenseTracker.Application.Features.Expenses.Queries.ListExpenses;
using ExpenseTracker.Application.Features.Weather.Queries.GetWeather;
using ExpenseTracker.Domain.Enums;
using FluentAssertions;

namespace ExpenseTracker.Application.Tests;

public class ValidatorTests
{
    private sealed class FixedClock(DateOnly today) : IAppClock
    {
        public DateOnly Today { get; } = today;
    }

    private static readonly DateOnly Today = new(2026, 9, 30);
    private readonly CreateExpenseCommandValidator _create = new(new FixedClock(Today));

    private static CreateExpenseCommand Valid() => new(85m, ExpenseCategory.Food, "kahve", Today);

    [Fact]
    public void A_normal_expense_is_valid() => _create.Validate(Valid()).IsValid.Should().BeTrue();

    [Fact]
    public void Tomorrow_is_allowed_for_clients_ahead_of_the_server_zone() =>
        _create.Validate(Valid() with { SpentOn = Today.AddDays(1) }).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("0", "Amount")]
    [InlineData("1000000.01", "Amount")]
    [InlineData("10.123", "Amount")]
    public void Invalid_amounts_are_reported_on_the_amount_field(string amount, string field)
    {
        var result = _create.Validate(Valid() with
        {
            Amount = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)
        });

        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be(field);
    }

    [Fact]
    public void Dates_further_in_the_future_are_rejected() =>
        _create.Validate(Valid() with { SpentOn = Today.AddDays(2) })
            .Errors.Should().ContainSingle(e => e.PropertyName == "SpentOn");

    [Fact]
    public void Long_notes_and_unknown_categories_are_rejected()
    {
        var result = _create.Validate(Valid() with { Note = new string('x', 201), Category = (ExpenseCategory)99 });

        result.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(["Note", "Category"]);
    }

    [Theory]
    [InlineData("2026-09", true)]
    [InlineData("09-2026", false)]
    [InlineData("", false)]
    public void Budget_month_must_be_yyyy_MM(string month, bool valid) =>
        new SetBudgetCommandValidator().Validate(new SetBudgetCommand(month, 3000m)).IsValid.Should().Be(valid);

    [Fact]
    public void Budget_amount_must_be_positive() =>
        new SetBudgetCommandValidator().Validate(new SetBudgetCommand("2026-09", 0m)).IsValid.Should().BeFalse();

    [Fact]
    public void Listing_without_a_month_is_valid_but_a_malformed_month_is_not()
    {
        var validator = new ListExpensesQueryValidator();

        validator.Validate(new ListExpensesQuery(null)).IsValid.Should().BeTrue();
        validator.Validate(new ListExpensesQuery("2026/09")).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Weather_date_cannot_be_far_in_the_future()
    {
        var validator = new GetWeatherQueryValidator(new FixedClock(Today));

        validator.Validate(new GetWeatherQuery(null)).IsValid.Should().BeTrue();
        validator.Validate(new GetWeatherQuery(Today.AddDays(5)))
            .Errors.Should().ContainSingle(e => e.PropertyName == "Date");
    }
}
