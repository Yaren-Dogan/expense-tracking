using System.Net;
using System.Net.Http.Json;
using ExpenseTracker.Application.Features.Expenses;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ExpensesApiTests(ApiFactory factory)
{
    private static object Coffee(string spentOn = "2026-09-01") =>
        new { amount = 85.50m, category = "Food", note = "kahve", spentOn };

    [Fact]
    public async Task Requests_without_a_session_are_rejected()
    {
        var anonymous = factory.CreateClient();

        (await anonymous.GetAsync("/api/expenses")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostExpense(Coffee())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Health_does_not_need_a_user()
    {
        (await factory.CreateClient().GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Created_expense_is_listed_under_its_month()
    {
        var client = await factory.ForNewUserAsync();

        var created = await client.PostExpense(Coffee());
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var expense = await created.Read<ExpenseDto>();

        var list = await client.GetFromJsonAsync<List<ExpenseDto>>("/api/expenses?month=2026-09", TestClient.Json);
        list.Should().ContainSingle().Which.Should().BeEquivalentTo(expense);

        var otherMonth = await client.GetFromJsonAsync<List<ExpenseDto>>("/api/expenses?month=2026-08", TestClient.Json);
        otherMonth.Should().BeEmpty();
    }

    [Fact]
    public async Task Users_cannot_see_or_delete_each_others_expenses()
    {
        var owner = await factory.ForNewUserAsync();
        var stranger = await factory.ForNewUserAsync();
        var expense = await (await owner.PostExpense(Coffee())).Read<ExpenseDto>();

        (await stranger.GetFromJsonAsync<List<ExpenseDto>>("/api/expenses?month=2026-09", TestClient.Json))
            .Should().BeEmpty();
        (await stranger.DeleteAsync($"/api/expenses/{expense.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await owner.DeleteAsync($"/api/expenses/{expense.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await owner.DeleteAsync($"/api/expenses/{expense.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Same_idempotency_key_returns_the_first_expense()
    {
        var client = await factory.ForNewUserAsync();
        var key = Guid.NewGuid().ToString();

        var first = await client.PostExpense(Coffee(), key);
        var second = await client.PostExpense(Coffee(), key);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Read<ExpenseDto>()).Id.Should().Be((await first.Read<ExpenseDto>()).Id);
    }

    [Fact]
    public async Task Concurrent_requests_with_one_idempotency_key_create_a_single_expense()
    {
        var client = await factory.ForNewUserAsync();
        var key = Guid.NewGuid().ToString();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PostExpense(Coffee(), key)));

        responses.Should().OnlyContain(r => r.IsSuccessStatusCode);
        var ids = await Task.WhenAll(responses.Select(r => r.Read<ExpenseDto>()));
        ids.Select(e => e.Id).Distinct().Should().ContainSingle();

        (await client.GetFromJsonAsync<List<ExpenseDto>>("/api/expenses?month=2026-09", TestClient.Json))
            .Should().ContainSingle();
    }

    [Fact]
    public async Task Idempotency_keys_are_scoped_per_user()
    {
        var key = Guid.NewGuid().ToString();

        var a = await (await factory.ForNewUserAsync()).PostExpense(Coffee(), key);
        var b = await (await factory.ForNewUserAsync()).PostExpense(Coffee(), key);

        a.StatusCode.Should().Be(HttpStatusCode.Created);
        b.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Invalid_input_returns_field_level_problem_details()
    {
        var response = await (await factory.ForNewUserAsync()).PostExpense(new
        {
            amount = 0,
            category = "Food",
            note = new string('x', 201),
            spentOn = "2026-09-01"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Read<ValidationProblemDetails>();
        problem.Errors.Keys.Should().BeEquivalentTo(["amount", "note"]);
    }

    [Fact]
    public async Task Unknown_category_is_rejected_by_model_binding()
    {
        var response = await (await factory.ForNewUserAsync()).PostExpense(new { amount = 10, category = "Yacht", spentOn = "2026-09-01" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_month_exports_as_a_csv_excel_opens_with_turkish_characters()
    {
        var client = await factory.ForNewUserAsync("Yaren");
        var outsider = await factory.ForNewUserAsync();
        (await client.PostExpense(new { amount = 1234.5m, category = "Market", note = "Migros; haftalık", spentOn = "2026-09-12" })).EnsureSuccessStatusCode();
        (await client.PostExpense(new { amount = 85m, category = "Food", note = "=HYPERLINK(\"http://x\")", spentOn = "2026-09-03" })).EnsureSuccessStatusCode();
        (await client.PostExpense(new { amount = 40m, category = "Transport", note = "ağustos", spentOn = "2026-08-31" })).EnsureSuccessStatusCode();

        var response = await client.GetAsync("/api/expenses/export?month=2026-09");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        response.Content.Headers.ContentDisposition!.FileName.Should().Be("harcamalar-2026-09.csv");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Take(3).Should().Equal([0xEF, 0xBB, 0xBF], "Excel needs the BOM to read UTF-8");
        var lines = System.Text.Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        lines.Should().Equal(
            "Tarih;Kategori;Tutar (TL);Not;Giren",
            "03.09.2026;Yemek;85,00;\"'=HYPERLINK(\"\"http://x\"\")\";Yaren",
            "12.09.2026;Market;1234,50;\"Migros; haftalık\";Yaren");

        var foreignBytes = await outsider.GetByteArrayAsync("/api/expenses/export?month=2026-09");
        var foreign = System.Text.Encoding.UTF8.GetString(foreignBytes[3..]);
        foreign.Trim().Should().Be("Tarih;Kategori;Tutar (TL);Not;Giren", "another ledger's expenses never appear");
        (await client.GetAsync("/api/expenses/export?month=2026-13")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
