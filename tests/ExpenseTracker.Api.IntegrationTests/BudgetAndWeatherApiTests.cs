using System.Net;
using System.Net.Http.Json;
using ExpenseTracker.Application.Features.Budgets;
using ExpenseTracker.Application.Features.Weather.Queries.GetWeather;
using ExpenseTracker.Domain.Weather;
using FluentAssertions;

namespace ExpenseTracker.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class BudgetAndWeatherApiTests(ApiFactory factory)
{
    [Fact]
    public async Task Budget_is_created_then_replaced()
    {
        var client = factory.ForNewUser();

        (await client.GetAsync("/api/budgets/2026-09")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await client.PutAsJsonAsync("/api/budgets/2026-09", new { amount = 3000 })).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync("/api/budgets/2026-09", new { amount = 4500 })).EnsureSuccessStatusCode();

        var budget = await client.GetFromJsonAsync<BudgetDto>("/api/budgets/2026-09", TestClient.Json);
        budget.Should().Be(new BudgetDto("2026-09", 4500m));
    }

    [Fact]
    public async Task Concurrent_first_budget_saves_end_with_one_row()
    {
        var client = factory.ForNewUser();

        var responses = await Task.WhenAll(Enumerable.Range(0, 6)
            .Select(i => client.PutAsJsonAsync("/api/budgets/2026-10", new { amount = 1000 + i })));

        responses.Should().OnlyContain(r => r.IsSuccessStatusCode);
        (await client.GetAsync("/api/budgets/2026-10")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Malformed_month_is_a_validation_error()
    {
        var response = await factory.ForNewUser().PutAsJsonAsync("/api/budgets/september", new { amount = 3000 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Weather_without_a_budget_has_no_conditions_but_still_totals_spending()
    {
        var client = factory.ForNewUser();
        await client.PostExpense(new { amount = 40, category = "Market", spentOn = "2026-09-05" });

        var weather = await client.GetFromJsonAsync<WeatherDto>("/api/weather?date=2026-09-05", TestClient.Json);

        weather!.Budget.Should().BeNull();
        weather.Today.Should().BeNull();
        weather.Forecast.Should().BeNull();
        weather.SpentThisMonth.Should().Be(40m);
        weather.LastSevenDays.Should().HaveCount(7).And.OnlyContain(d => d.Condition == null);
    }

    [Fact]
    public async Task Weather_follows_the_prd_rules()
    {
        var client = factory.ForNewUser();
        await client.PutAsJsonAsync("/api/budgets/2026-09", new { amount = 3000 });
        // 1st: 50 of a 100 allowance = sunny. 2nd: 2950 left over 29 days (101.72), 200 spent = stormy.
        await client.PostExpense(new { amount = 50, category = "Food", spentOn = "2026-09-01" });
        await client.PostExpense(new { amount = 200, category = "Entertainment", spentOn = "2026-09-02" });

        var weather = await client.GetFromJsonAsync<WeatherDto>("/api/weather?date=2026-09-02", TestClient.Json);

        weather!.Today!.Condition.Should().Be(WeatherCondition.Stormy);
        weather.Today.DailyAllowance.Should().Be(101.72m);
        weather.Today.SafeToSpend.Should().Be(0m);
        weather.SpentThisMonth.Should().Be(250m);
        weather.RemainingBudget.Should().Be(2750m);

        var strip = weather.LastSevenDays;
        strip.Select(d => d.Date).Should().BeInAscendingOrder().And.HaveCount(7);
        strip.Single(d => d.Date == new DateOnly(2026, 9, 1)).Condition.Should().Be(WeatherCondition.Sunny);
        // August days have no budget in this test, so they carry no weather.
        strip.Where(d => d.Date.Month == 8).Should().OnlyContain(d => d.Condition == null);

        // 250 over 2 days = 125/day, 3750 projected: 1.25x the budget.
        weather.Forecast!.ProjectedSpend.Should().Be(3750m);
        weather.Forecast.Condition.Should().Be(WeatherCondition.Rainy);
    }

    [Fact]
    public async Task Weather_rejects_dates_far_in_the_future()
    {
        var response = await factory.ForNewUser().GetAsync("/api/weather?date=2999-01-01");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
