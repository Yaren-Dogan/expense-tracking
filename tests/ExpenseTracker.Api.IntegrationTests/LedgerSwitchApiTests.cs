using System.Net;
using System.Net.Http.Json;
using ExpenseTracker.Api.Contracts;
using ExpenseTracker.Application.Features.Budgets;
using ExpenseTracker.Application.Features.Expenses;
using ExpenseTracker.Application.Features.Households;
using ExpenseTracker.Application.Features.Tasks;
using FluentAssertions;

namespace ExpenseTracker.Api.IntegrationTests;

/// <summary>A household member can work in their personal ledger without leaving the household (X-Ledger: personal).</summary>
[Collection(ApiCollection.Name)]
public class LedgerSwitchApiTests(ApiFactory factory)
{
    private sealed record Family(HttpClient Yaren, HttpClient Anil, Guid YarenId, Guid AnilId);

    private static object Expense(decimal amount, string note) => new { amount, category = "Market", note, spentOn = "2026-09-20" };

    /// <summary>Anıl spends ₺700 personally before joining Yaren's household.</summary>
    private async Task<Family> Couple()
    {
        var yaren = await factory.ForNewUserAsync("Yaren");
        var anil = await factory.ForNewUserAsync("Anıl");
        (await anil.PostExpense(Expense(700, "Kişisel, katılmadan önce"))).EnsureSuccessStatusCode();
        var household = await (await yaren.PostAsJsonAsync("/api/households", new { name = "Test Ailesi" })).Read<HouseholdDto>();
        var joined = await (await anil.PostAsJsonAsync("/api/households/join", new { code = household.InviteCode })).Read<HouseholdDto>();
        return new Family(yaren, anil, joined.Members.Single(m => m.DisplayName == "Yaren").UserId, joined.Members.Single(m => m.DisplayName == "Anıl").UserId);
    }

    private static Task<List<ExpenseDto>?> September(HttpClient client) =>
        client.GetFromJsonAsync<List<ExpenseDto>>("/api/expenses?month=2026-09", TestClient.Json);

    [Fact]
    public async Task Switching_shows_the_personal_ledger_and_keeps_it_private()
    {
        var f = await Couple();
        (await f.Yaren.PostExpense(Expense(300, "Ortak market"))).EnsureSuccessStatusCode();

        (await September(f.Anil))!.Select(e => e.Note).Should().Equal("Ortak market");
        (await f.Anil.GetFromJsonAsync<MeResponse>("/api/account/me", TestClient.Json))!.Ledger.Should().Be("household");

        f.Anil.UsePersonalLedger();
        var me = await f.Anil.GetFromJsonAsync<MeResponse>("/api/account/me", TestClient.Json);
        me!.Ledger.Should().Be("personal");
        me.Household!.Name.Should().Be("Test Ailesi", "switching does not leave the household");
        (await September(f.Anil))!.Select(e => e.Note).Should().Equal("Kişisel, katılmadan önce");

        (await f.Anil.PostExpense(Expense(50, "Kişisel kahve"))).EnsureSuccessStatusCode();
        (await f.Anil.PutAsJsonAsync("/api/budgets/2026-09", new { amount = 2000 })).EnsureSuccessStatusCode();

        // Yaren, in the household ledger, sees neither Anıl's personal expenses nor his personal budget.
        (await September(f.Yaren))!.Select(e => e.Note).Should().Equal("Ortak market");
        (await f.Yaren.GetAsync("/api/budgets/2026-09")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        f.Anil.UseDefaultLedger();
        (await September(f.Anil))!.Select(e => e.Note).Should().Equal("Ortak market");
        (await f.Anil.GetAsync("/api/budgets/2026-09")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_unknown_header_value_falls_back_to_the_household_and_without_a_household_it_is_personal_anyway()
    {
        var f = await Couple();
        f.Anil.DefaultRequestHeaders.Add("X-Ledger", "baskasinin-defteri");
        (await f.Anil.GetFromJsonAsync<MeResponse>("/api/account/me", TestClient.Json))!.Ledger.Should().Be("household");

        var solo = (await factory.ForNewUserAsync("Tek")).UsePersonalLedger();
        (await solo.GetFromJsonAsync<MeResponse>("/api/account/me", TestClient.Json))!.Ledger.Should().Be("personal");
        (await solo.PostExpense(Expense(10, "x"))).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task In_the_personal_ledger_tasks_are_personal()
    {
        var f = await Couple();
        (await f.Yaren.PostAsJsonAsync("/api/tasks", new { title = "Ortak fatura", assigneeId = f.AnilId, dueOn = "2026-10-10" })).EnsureSuccessStatusCode();

        f.Anil.UsePersonalLedger();
        (await f.Anil.GetFromJsonAsync<TaskListDto>("/api/tasks", TestClient.Json))!.Open.Should().BeEmpty();
        var toPartner = await f.Anil.PostAsJsonAsync("/api/tasks", new { title = "Yaren'e", assigneeId = f.YarenId, dueOn = "2026-10-10" });
        toPartner.StatusCode.Should().Be(HttpStatusCode.BadRequest, "only the user is in their personal ledger");
        (await f.Anil.PostAsJsonAsync("/api/tasks", new { title = "Telefon", assigneeId = f.AnilId, dueOn = "2026-10-10" })).StatusCode
            .Should().Be(HttpStatusCode.Created);

        f.Anil.UseDefaultLedger();
        (await f.Anil.GetFromJsonAsync<TaskListDto>("/api/tasks", TestClient.Json))!.Open.Select(t => t.Title).Should().Equal("Ortak fatura");
    }

    [Fact]
    public async Task Leaving_from_the_personal_view_still_hands_household_tasks_to_the_owner()
    {
        var f = await Couple();
        var task = await (await f.Yaren.PostAsJsonAsync("/api/tasks", new { title = "İnternet", assigneeId = f.AnilId, dueOn = "2026-10-10" })).Read<TaskDto>();

        f.Anil.UsePersonalLedger();
        (await f.Anil.PostAsync("/api/households/leave", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await f.Yaren.GetFromJsonAsync<TaskListDto>("/api/tasks", TestClient.Json))!.Open
            .Should().ContainSingle(t => t.Id == task.Id && t.AssigneeId == f.YarenId);
    }
}
