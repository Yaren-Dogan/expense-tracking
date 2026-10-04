using System.Net;
using System.Net.Http.Json;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Features.Assistant.Commands.AskAssistant;
using ExpenseTracker.Application.Features.Expenses;
using ExpenseTracker.Application.Features.Expenses.Queries.ParseExpense;
using ExpenseTracker.Domain.Enums;
using ExpenseTracker.Infrastructure.Ai;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseTracker.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AiApiTests : IDisposable
{
    private readonly ApiFactory _factory;

    public AiApiTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.Model.Reset();
    }

    public void Dispose() => _factory.Model.Reset();

    private static object Expense(decimal amount, string category, string note, string spentOn) =>
        new { amount, category, note, spentOn };

    private async Task<List<ExpenseMatch>> SearchUntilIndexed(HttpClient client, string query, int expected)
    {
        // The test clock is frozen, so the background timer never ticks: run an indexing pass directly.
        var indexer = _factory.Services.GetRequiredService<ExpenseEmbeddingIndexer>();
        for (var attempt = 0; attempt < 10; attempt++)
        {
            while (await indexer.IndexBatchAsync(default) > 0) { }
            var matches = await client.GetFromJsonAsync<List<ExpenseMatch>>(
                $"/api/expenses/search?q={Uri.EscapeDataString(query)}", TestClient.Json);
            if (matches!.Count >= expected) return matches;
            await Task.Delay(500);
        }

        throw new TimeoutException($"Search for '{query}' never returned {expected} results.");
    }

    [Fact]
    public async Task Parse_returns_a_suggestion_without_saving_it()
    {
        var client = await _factory.ForNewUserAsync();
        _factory.Model.Reply("""{"amount":85,"category":"Food","daysAgo":1,"note":"Kahve"}""");

        var response = await client.PostAsJsonAsync("/api/expenses/parse", new { text = "dün kahveye 85", today = "2026-09-30" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Read<ParsedExpenseDto>()).Should()
            .Be(new ParsedExpenseDto(85m, ExpenseCategory.Food, "Kahve", new DateOnly(2026, 9, 29)));
        (await client.GetFromJsonAsync<List<ExpenseDto>>("/api/expenses?month=2026-09", TestClient.Json))
            .Should().BeEmpty("parsing only suggests; the user confirms by saving");
    }

    [Fact]
    public async Task Parse_maps_model_failures_to_422_and_503()
    {
        var client = await _factory.ForNewUserAsync();
        _factory.Model.Reply("I don't know");
        _factory.Model.Fail(new HttpRequestException("connection refused"));

        (await client.PostAsJsonAsync("/api/expenses/parse", new { text = "hmm" }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await client.PostAsJsonAsync("/api/expenses/parse", new { text = "kahve 85" }))
            .StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await client.PostAsJsonAsync("/api/expenses/parse", new { text = "" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Semantic_search_ranks_related_expenses_and_never_crosses_users()
    {
        var owner = await _factory.ForNewUserAsync();
        var stranger = await _factory.ForNewUserAsync();
        await owner.PostExpense(Expense(95, "Food", "Kahve Dünyası", "2026-09-10"));
        await owner.PostExpense(Expense(85, "Food", "Kahveci", "2026-09-11"));
        await owner.PostExpense(Expense(310, "Transport", "Taksi", "2026-09-12"));
        await stranger.PostExpense(Expense(50, "Food", "Kahve", "2026-09-12"));

        var matches = await SearchUntilIndexed(owner, "kahve", expected: 2);

        matches.Select(m => m.Expense.Note).Should().BeEquivalentTo(["Kahve Dünyası", "Kahveci"]);
        matches.Should().BeInDescendingOrder(m => m.Similarity);
        (await SearchUntilIndexed(stranger, "kahve", expected: 1)).Should().ContainSingle()
            .Which.Expense.Amount.Should().Be(50m);
    }

    [Fact]
    public async Task Deleted_expenses_drop_out_of_the_index()
    {
        var client = await _factory.ForNewUserAsync();
        var expense = await (await client.PostExpense(Expense(120, "Entertainment", "Sinema bileti", "2026-09-05"))).Read<ExpenseDto>();
        await SearchUntilIndexed(client, "sinema", expected: 1);

        await client.DeleteAsync($"/api/expenses/{expense.Id}");

        (await client.GetFromJsonAsync<List<ExpenseMatch>>("/api/expenses/search?q=sinema", TestClient.Json))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task Assistant_runs_the_chosen_tool_on_the_callers_data_and_reports_it()
    {
        var client = await _factory.ForNewUserAsync();
        await client.PostExpense(Expense(2000, "Market", "Migros", "2026-09-03"));
        await client.PostExpense(Expense(300, "Food", "Döner", "2026-09-04"));
        // Another user's big spending must not leak into this user's numbers.
        await (await _factory.ForNewUserAsync()).PostExpense(Expense(99999, "Clothing", "Kürk", "2026-09-04"));

        _factory.Model.Reply("""{"reason":"kategori toplamı lazım","action":"GetSpendingByCategory","from":"2026-09-01","to":"2026-09-30","category":null,"query":null}""");
        _factory.Model.Reply("""{"reason":"yeterli","action":"Answer","from":null,"to":null,"category":null,"query":null}""");

        var response = await client.PostAsJsonAsync("/api/assistant/chat", new
        {
            messages = new[] { new { role = "user", content = "Eylülde en çok nereye harcadım?" } },
            today = "2026-09-30"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var reply = await response.Read<AssistantReply>();
        reply.ToolsUsed.Should().Equal("get_spending_by_category");
        // The model chose the tool; the numbers come from code over this user's rows only.
        reply.Reply.Should().Be(
            "2026-09-01 – 2026-09-30 arasında toplam ₺2.300,00 harcandı. En çok harcanan kategori Market: ₺2.000,00 " +
            "(payı %87). İkinci sırada Yemek: ₺300,00.");
        _factory.Model.Requests.Should().HaveCount(2, "two planning steps; the reply itself needs no model call");
    }

    [Fact]
    public async Task Assistant_rejects_tool_arguments_it_cannot_run_and_lets_the_model_recover()
    {
        var client = await _factory.ForNewUserAsync();
        _factory.Model.Reply("""{"reason":"x","action":"ListExpenses","from":"dün","to":"bugün","category":null,"query":null}""");
        _factory.Model.Reply("""{"reason":"x","action":"Answer","from":null,"to":null,"category":null,"query":null}""");
        _factory.Model.Reply("Tarihleri anlayamadım.");

        var reply = await (await client.PostAsJsonAsync("/api/assistant/chat", new
        {
            messages = new[] { new { role = "user", content = "dünkü harcamalar?" } }
        })).Read<AssistantReply>();

        reply.ToolsUsed.Should().Equal("list_expenses");
        _factory.Model.Requests[1].Last().Text.Should().Contain("yyyy-MM-dd", "the tool error is fed back to the model");
    }

    [Fact]
    public async Task Assistant_validates_the_conversation()
    {
        var response = await (await _factory.ForNewUserAsync()).PostAsJsonAsync("/api/assistant/chat", new
        {
            messages = new[] { new { role = "assistant", content = "Merhaba" } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _factory.Model.Requests.Should().BeEmpty("invalid requests never reach the model");
    }
}
