using ExpenseTracker.Application.Common.Exceptions;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Features.Expenses.Queries.ParseExpense;
using ExpenseTracker.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExpenseTracker.Application.Tests;

public class ParseExpenseTests
{
    private static readonly DateOnly Today = new(2026, 10, 1);

    private sealed class FixedClock : IAppClock
    {
        public DateOnly Today => ParseExpenseTests.Today;
    }

    private static (ParseExpenseQueryHandler Handler, ScriptedChatClient Model) Create(string reply)
    {
        var model = new ScriptedChatClient(reply);
        return (new ParseExpenseQueryHandler(model, new FixedClock(), NullLogger<ParseExpenseQueryHandler>.Instance), model);
    }

    [Fact]
    public async Task Code_turns_days_ago_into_a_date()
    {
        var (handler, _) = Create("""{"amount":85,"category":"Food","daysAgo":1,"note":"Kahve"}""");

        var result = await handler.Handle(new ParseExpenseQuery("dün kahveye 85 lira", null), default);

        result.Should().Be(new ParsedExpenseDto(85m, ExpenseCategory.Food, "Kahve", new DateOnly(2026, 9, 30)));
    }

    [Fact]
    public async Task The_clients_today_wins_over_the_servers()
    {
        var (handler, _) = Create("""{"amount":10,"category":"Other","daysAgo":0,"note":null}""");

        var result = await handler.Handle(new ParseExpenseQuery("10 lira", new DateOnly(2026, 10, 2)), default);

        result.SpentOn.Should().Be(new DateOnly(2026, 10, 2));
    }

    [Fact]
    public async Task Out_of_range_values_from_the_model_are_clamped()
    {
        var (handler, _) = Create("""{"amount":12.3456,"category":"Market","daysAgo":-4,"note":"  Migros  "}""");

        var result = await handler.Handle(new ParseExpenseQuery("migros", null), default);

        result.Amount.Should().Be(12.35m);
        result.SpentOn.Should().Be(Today, "a negative daysAgo would be a future date");
        result.Note.Should().Be("Migros");
    }

    [Theory]
    [InlineData("""{"amount":0,"category":"Food","daysAgo":0,"note":"x"}""")]
    [InlineData("this is not json")]
    public async Task Unusable_model_output_is_a_422_not_a_crash(string reply)
    {
        var (handler, _) = Create(reply);

        var act = () => handler.Handle(new ParseExpenseQuery("bir şey", null), default);

        await act.Should().ThrowAsync<AiResponseException>();
    }

    [Fact]
    public async Task The_model_gets_todays_date_a_json_schema_and_zero_temperature()
    {
        var (handler, model) = Create("""{"amount":1,"category":"Other","daysAgo":0,"note":null}""");

        await handler.Handle(new ParseExpenseQuery("1 lira", null), default);

        model.Requests.Single()[0].Text.Should().Contain("2026-10-01").And.Contain("Perşembe");
        model.Options.Single()!.Temperature.Should().Be(0);
        model.Options.Single()!.ResponseFormat.Should().BeOfType<ChatResponseFormatJson>()
            .Which.Schema.ToString().Should().Contain("Clothing", "the category enum must be part of the schema")
            .And.NotContain("null", "some providers (e.g. Gemini) reject nullable type unions");
    }
}
