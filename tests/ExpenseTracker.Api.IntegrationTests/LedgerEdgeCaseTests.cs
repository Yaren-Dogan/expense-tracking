using System.Net;
using System.Net.Http.Json;
using ExpenseTracker.Api.Contracts;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Features.Assistant.Commands.AskAssistant;
using ExpenseTracker.Application.Features.Budgets;
using ExpenseTracker.Application.Features.Expenses;
using ExpenseTracker.Application.Features.Households;
using ExpenseTracker.Application.Features.Insights.Plans;
using ExpenseTracker.Application.Features.Insights.Reviews;
using ExpenseTracker.Application.Features.Notifications;
using ExpenseTracker.Application.Features.Tasks;
using ExpenseTracker.Application.Features.Weather.Queries.GetWeather;
using ExpenseTracker.Domain.Notifications;
using ExpenseTracker.Infrastructure.Ai;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseTracker.Api.IntegrationTests;

/// <summary>
/// Edge cases of the ledger switcher (X-Ledger: personal): nothing may cross between a member's personal ledger and their
/// household's, in either direction, while things addressed to the person (notifications) reach them in both views.
/// The test clock is pinned to 2026-09-30: August is the last finished month, October the first planned one.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class LedgerEdgeCaseTests : IDisposable
{
    private readonly ApiFactory _factory;

    public LedgerEdgeCaseTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.Model.Reset();
    }

    public void Dispose() => _factory.Model.Reset();

    private sealed record Family(HttpClient Zeynep, HttpClient Mert, Guid ZeynepId, Guid MertId, HouseholdDto Household);

    /// <summary>Zeynep founds "Kaya ailesi", Mert joins. Both clients start in the household ledger.</summary>
    private async Task<Family> Kayalar()
    {
        var zeynep = await _factory.ForNewUserAsync("Zeynep");
        var mert = await _factory.ForNewUserAsync("Mert");
        var household = await (await zeynep.PostAsJsonAsync("/api/households", new { name = "Kaya ailesi" })).Read<HouseholdDto>();
        var joined = await (await mert.PostAsJsonAsync("/api/households/join", new { code = household.InviteCode })).Read<HouseholdDto>();
        return new Family(zeynep, mert,
            joined.Members.Single(m => m.DisplayName == "Zeynep").UserId,
            joined.Members.Single(m => m.DisplayName == "Mert").UserId,
            joined);
    }

    private static object Expense(decimal amount, string category, string note, string spentOn) => new { amount, category, note, spentOn };

    private static async Task<ExpenseDto> Spend(HttpClient client, decimal amount, string category, string note, string spentOn)
    {
        var response = await client.PostExpense(Expense(amount, category, note, spentOn));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await response.Read<ExpenseDto>();
    }

    private static async Task<List<ExpenseDto>> Month(HttpClient client, string month = "2026-09") =>
        (await client.GetFromJsonAsync<List<ExpenseDto>>($"/api/expenses?month={month}", TestClient.Json))!;

    private static async Task<TaskDto> CreateTask(HttpClient client, string title, Guid assignee, string dueOn)
    {
        var response = await client.PostAsJsonAsync("/api/tasks", new { title, assigneeId = assignee, dueOn });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await response.Read<TaskDto>();
    }

    private static async Task<TaskListDto> Tasks(HttpClient client) =>
        (await client.GetFromJsonAsync<TaskListDto>("/api/tasks", TestClient.Json))!;

    private static async Task<NotificationListDto> Notifications(HttpClient client) =>
        (await client.GetFromJsonAsync<NotificationListDto>("/api/notifications", TestClient.Json))!;

    [Fact]
    public async Task Household_expenses_and_tasks_cannot_be_touched_from_the_personal_view()
    {
        var f = await Kayalar();
        var ownHouseholdExpense = await Spend(f.Mert, 450, "Market", "Ortak market", "2026-09-20");
        var task = await CreateTask(f.Zeynep, "Elektrik faturası", f.MertId, "2026-10-10");

        f.Mert.UsePersonalLedger();
        (await f.Mert.DeleteAsync($"/api/expenses/{ownHouseholdExpense.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "the household ledger is not the one he is working in, even for his own expense");
        (await f.Mert.PostAsJsonAsync($"/api/tasks/{task.Id}/complete", new { amount = 900 })).StatusCode.Should().Be(HttpStatusCode.NotFound);

        f.Zeynep.UsePersonalLedger();
        (await f.Zeynep.DeleteAsync($"/api/tasks/{task.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "the owner and creator is in her personal ledger");

        f.Mert.UseDefaultLedger();
        f.Zeynep.UseDefaultLedger();
        (await Month(f.Zeynep)).Should().ContainSingle(e => e.Id == ownHouseholdExpense.Id, "nothing was deleted");
        (await Tasks(f.Mert)).Open.Should().ContainSingle(t => t.Id == task.Id, "nothing was completed or deleted");
        (await Month(f.Mert)).Should().HaveCount(1, "the refused completion recorded no payment");
    }

    [Fact]
    public async Task Personal_expenses_and_tasks_cannot_be_touched_from_the_household_view()
    {
        var f = await Kayalar();
        f.Mert.UsePersonalLedger();
        var personalExpense = await Spend(f.Mert, 80, "Food", "Kişisel kahve", "2026-09-21");
        var personalTask = await CreateTask(f.Mert, "Telefon faturası", f.MertId, "2026-10-03");

        f.Mert.UseDefaultLedger();
        (await f.Mert.DeleteAsync($"/api/expenses/{personalExpense.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await f.Mert.PostAsJsonAsync($"/api/tasks/{personalTask.Id}/complete", new { })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await f.Mert.DeleteAsync($"/api/tasks/{personalTask.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await f.Zeynep.DeleteAsync($"/api/tasks/{personalTask.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "being the household owner gives no reach into a member's personal ledger");

        f.Mert.UsePersonalLedger();
        (await Month(f.Mert)).Should().ContainSingle(e => e.Id == personalExpense.Id);
        (await Tasks(f.Mert)).Open.Should().ContainSingle(t => t.Id == personalTask.Id);
    }

    [Fact]
    public async Task Paying_a_personal_task_records_the_expense_in_the_personal_ledger()
    {
        var f = await Kayalar();
        f.Mert.UsePersonalLedger();
        var task = await CreateTask(f.Mert, "Spor salonu", f.MertId, "2026-10-01");

        var completed = await f.Mert.PostAsJsonAsync($"/api/tasks/{task.Id}/complete", new { amount = 1250 });
        completed.StatusCode.Should().Be(HttpStatusCode.OK, await completed.Content.ReadAsStringAsync());

        (await Month(f.Mert)).Should().ContainSingle().Which.Should().BeEquivalentTo(new { Amount = 1250m, Note = "Spor salonu", CreatedBy = f.MertId });
        (await Month(f.Zeynep)).Should().BeEmpty("a personal bill never lands in the household");
        (await Notifications(f.Zeynep)).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task The_owner_can_remove_a_member_from_her_personal_view_and_his_open_tasks_come_to_her()
    {
        var f = await Kayalar();
        var open = await CreateTask(f.Zeynep, "İnternet faturası", f.MertId, "2026-10-12");
        f.Mert.UsePersonalLedger();
        var mertsOwn = await CreateTask(f.Mert, "Kişisel hatırlatma", f.MertId, "2026-10-12");

        f.Zeynep.UsePersonalLedger();
        var removed = await f.Zeynep.DeleteAsync($"/api/households/members/{f.MertId}");
        removed.StatusCode.Should().Be(HttpStatusCode.OK, await removed.Content.ReadAsStringAsync());
        (await removed.Read<HouseholdDto>()).Members.Should().ContainSingle(m => m.UserId == f.ZeynepId);

        f.Zeynep.UseDefaultLedger();
        (await Tasks(f.Zeynep)).Open.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { open.Id, AssigneeId = f.ZeynepId }, "the household task was handed over, his personal one was not");
        (await Tasks(f.Mert)).Open.Should().ContainSingle(t => t.Id == mertsOwn.Id && t.AssigneeId == f.MertId);
        (await Notifications(f.Zeynep)).Items.Should().ContainSingle(n => n.SubjectId == open.Id && n.Message.StartsWith("Mert haneden ayrıldı"));
    }

    [Fact]
    public async Task Notifications_reach_the_person_whichever_ledger_they_are_looking_at()
    {
        var f = await Kayalar();
        f.Mert.UsePersonalLedger();
        var personal = await CreateTask(f.Mert, "Kredi kartı ödemesi", f.MertId, "2026-10-01");

        // Assigned while he is in his personal view; he must still be told.
        var household = await CreateTask(f.Zeynep, "Aidat", f.MertId, "2026-10-02");
        (await Notifications(f.Mert)).Items.Should().ContainSingle(n => n.Kind == NotificationKind.TaskAssigned && n.SubjectId == household.Id);

        await _factory.RunTaskRemindersAsync();

        // Both are due within 3 days of 30 Eylül: one reminder each, readable from either view.
        foreach (var view in new[] { true, false })
        {
            if (view) f.Mert.UsePersonalLedger(); else f.Mert.UseDefaultLedger();
            var items = (await Notifications(f.Mert)).Items;
            items.Should().ContainSingle(n => n.Kind == NotificationKind.TaskDueSoon && n.SubjectId == personal.Id);
            items.Should().ContainSingle(n => n.Kind == NotificationKind.TaskDueSoon && n.SubjectId == household.Id);
        }

        (await Notifications(f.Zeynep)).Items.Should().NotContain(n => n.SubjectId == personal.Id, "his personal task is nobody else's business");

        f.Mert.UsePersonalLedger();
        (await f.Mert.PostAsync("/api/notifications/read", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        f.Mert.UseDefaultLedger();
        (await Notifications(f.Mert)).UnreadCount.Should().Be(0, "marking read from the personal view clears them everywhere");
    }

    [Fact]
    public async Task Weather_and_budget_follow_the_active_ledger()
    {
        var f = await Kayalar();
        (await f.Zeynep.PutAsJsonAsync("/api/budgets/2026-09", new { amount = 30000 })).EnsureSuccessStatusCode();
        await Spend(f.Zeynep, 6000, "Bills", "Kira payı", "2026-09-10");
        f.Mert.UsePersonalLedger();
        (await f.Mert.PutAsJsonAsync("/api/budgets/2026-09", new { amount = 3000 })).EnsureSuccessStatusCode();
        await Spend(f.Mert, 250, "Food", "Kişisel öğle yemeği", "2026-09-29");

        var personal = await f.Mert.GetFromJsonAsync<WeatherDto>("/api/weather?date=2026-09-30", TestClient.Json);
        personal!.Budget.Should().Be(3000);
        personal.SpentThisMonth.Should().Be(250);
        personal.RemainingBudget.Should().Be(2750); // 3000 - 250

        f.Mert.UseDefaultLedger();
        var shared = await f.Mert.GetFromJsonAsync<WeatherDto>("/api/weather?date=2026-09-30", TestClient.Json);
        shared!.Budget.Should().Be(30000);
        shared.SpentThisMonth.Should().Be(6000);
        shared.RemainingBudget.Should().Be(24000); // 30000 - 6000
        (await f.Mert.GetFromJsonAsync<BudgetDto>("/api/budgets/2026-09", TestClient.Json))!.Amount.Should().Be(30000);
    }

    [Fact]
    public async Task Month_reviews_are_written_per_ledger_and_each_member_is_told_about_their_own()
    {
        var f = await Kayalar();
        await Spend(f.Zeynep, 5000, "Bills", "Kira payı", "2026-08-05");
        f.Mert.UsePersonalLedger();
        await Spend(f.Mert, 1200, "Clothing", "Kişisel mont", "2026-08-15");
        for (var i = 0; i < 600; i++)
            _factory.Model.Fail(new HttpRequestException("no model in this test"));

        await _factory.RunMonthReviewsAsync();

        var personal = await f.Mert.GetFromJsonAsync<MonthReviewDto>("/api/reviews/2026-08", TestClient.Json);
        f.Mert.UseDefaultLedger();
        var shared = await f.Mert.GetFromJsonAsync<MonthReviewDto>("/api/reviews/2026-08", TestClient.Json);

        personal!.Id.Should().NotBe(shared!.Id);
        personal.Highlights[0].Should().StartWith("Ağustos ayında toplam ₺1.200 harcandı", "only his personal coat");
        shared.Highlights[0].Should().StartWith("Ağustos ayında toplam ₺5.000 harcandı", "the personal coat is not household spending");

        foreach (var view in new[] { true, false })
        {
            if (view) f.Mert.UsePersonalLedger(); else f.Mert.UseDefaultLedger();
            (await Notifications(f.Mert)).Items.Where(n => n.Kind == NotificationKind.MonthReviewReady).Select(n => n.SubjectId)
                .Should().BeEquivalentTo([personal.Id, shared.Id], "he reads both ledgers, so both reviews are his");
        }

        (await Notifications(f.Zeynep)).Items.Where(n => n.Kind == NotificationKind.MonthReviewReady).Select(n => n.SubjectId)
            .Should().Equal([shared.Id], "Mert's personal review is not hers");
    }

    [Fact]
    public async Task A_personal_saving_plan_uses_personal_history_and_only_sets_personal_budgets()
    {
        var f = await Kayalar();
        await Spend(f.Zeynep, 20000, "Bills", "Kira", "2026-08-05");
        f.Mert.UsePersonalLedger();
        await Spend(f.Mert, 3000, "Entertainment", "Konser", "2026-08-20");

        _factory.Model.Fail(new HttpRequestException("down"));
        var response = await f.Mert.PostAsJsonAsync("/api/plans", new { goalAmount = 1000, months = 2, purpose = "Kulaklık" });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var plan = await response.Read<BudgetPlanDto>();
        // Only August has personal spending: baseline 3.000 a month; 1.000 over 2 months = 500 a month saved.
        plan.BaselineMonthly.Should().Be(3000);
        plan.MonthlyBudget.Should().Be(2500);

        f.Mert.UseDefaultLedger();
        (await f.Mert.GetAsync("/api/plans/latest")).StatusCode.Should().Be(HttpStatusCode.NoContent, "the household has no plan");
        (await f.Mert.PostAsync($"/api/plans/{plan.Id}/apply", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await f.Zeynep.PostAsync($"/api/plans/{plan.Id}/apply", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        f.Mert.UsePersonalLedger();
        (await f.Mert.PostAsync($"/api/plans/{plan.Id}/apply", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await f.Mert.GetFromJsonAsync<BudgetDto>("/api/budgets/2026-10", TestClient.Json))!.Amount.Should().Be(2500);
        (await f.Mert.GetFromJsonAsync<BudgetDto>("/api/budgets/2026-11", TestClient.Json))!.Amount.Should().Be(2500);

        (await f.Zeynep.GetAsync("/api/budgets/2026-10")).StatusCode.Should().Be(HttpStatusCode.NotFound, "the household budget is untouched");
        (await Notifications(f.Zeynep)).Items.Should().NotContain(n => n.Kind == NotificationKind.BudgetPlanApplied);
    }

    [Fact]
    public async Task The_assistant_answers_from_the_active_ledger_only()
    {
        var f = await Kayalar();
        await Spend(f.Zeynep, 9000, "Bills", "Kira", "2026-09-05");
        f.Mert.UsePersonalLedger();
        await Spend(f.Mert, 400, "Market", "Kişisel market", "2026-09-06");

        _factory.Model.Reply("""{"reason":"toplam lazım","action":"GetSpendingByCategory","from":"2026-09-01","to":"2026-09-30","category":null,"query":null}""");
        _factory.Model.Reply("""{"reason":"yeterli","action":"Answer","from":null,"to":null,"category":null,"query":null}""");
        var response = await f.Mert.PostAsJsonAsync("/api/assistant/chat", new
        {
            messages = new[] { new { role = "user", content = "Eylülde ne kadar harcadım?" } },
            today = "2026-09-30"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var reply = await response.Read<AssistantReply>();
        reply.Reply.Should().Contain("toplam ₺400,00").And.NotContain("9.000", "household rent is not his personal spending");
    }

    [Fact]
    public async Task Semantic_search_stays_inside_the_active_ledger()
    {
        var f = await Kayalar();
        await Spend(f.Zeynep, 120, "Food", "Ortak kahve", "2026-09-10");
        f.Mert.UsePersonalLedger();
        await Spend(f.Mert, 60, "Food", "Kişisel kahve", "2026-09-11");

        var personal = await SearchUntilIndexed(f.Mert, "kahve", expected: 1);
        personal.Select(m => m.Expense.Note).Should().Equal("Kişisel kahve");

        f.Mert.UseDefaultLedger();
        var shared = await SearchUntilIndexed(f.Mert, "kahve", expected: 1);
        shared.Select(m => m.Expense.Note).Should().Equal("Ortak kahve");
        (await SearchUntilIndexed(f.Zeynep, "kahve", expected: 1)).Select(m => m.Expense.Note).Should().Equal("Ortak kahve");
    }

    private async Task<List<ExpenseMatch>> SearchUntilIndexed(HttpClient client, string query, int expected)
    {
        // The test clock is frozen, so the background indexer never ticks: run indexing passes directly.
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
    public async Task Reusing_an_idempotency_key_in_the_other_ledger_replays_and_never_duplicates()
    {
        var f = await Kayalar();
        var key = Guid.NewGuid().ToString();
        f.Mert.UsePersonalLedger();
        var first = await f.Mert.PostExpense(Expense(75, "Food", "Simit ve çay", "2026-09-29"), key);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await first.Read<ExpenseDto>();

        // The same tap retried after switching views: it is the same request, so it is answered with the original.
        f.Mert.UseDefaultLedger();
        var retried = await f.Mert.PostExpense(Expense(75, "Food", "Simit ve çay", "2026-09-29"), key);
        retried.StatusCode.Should().Be(HttpStatusCode.OK);
        (await retried.Read<ExpenseDto>()).Id.Should().Be(created.Id);

        (await Month(f.Mert)).Should().BeEmpty("nothing was added to the household");
        f.Mert.UsePersonalLedger();
        (await Month(f.Mert)).Should().ContainSingle(e => e.Id == created.Id);
    }

    [Fact]
    public async Task Device_data_claimed_by_a_household_member_goes_to_the_personal_ledger()
    {
        var f = await Kayalar();
        var deviceId = Guid.NewGuid();
        await _factory.SeedDeviceExpenseAsync(deviceId, 120m, "2026-09-12");

        // Signing in again from the old device while in the household view.
        var login = await f.Mert.PostAsJsonAsync("/api/account/login", new { email = f.Mert.Email(), password = TestClient.Password, deviceUserId = deviceId });
        login.StatusCode.Should().Be(HttpStatusCode.OK, await login.Content.ReadAsStringAsync());
        (await login.Read<MeResponse>()).Should().BeEquivalentTo(new { ClaimedExpenses = 1, Ledger = "household" });

        (await Month(f.Mert)).Should().BeEmpty("device data is personal, never the household's");
        (await Month(f.Zeynep)).Should().BeEmpty();
        f.Mert.UsePersonalLedger();
        (await Month(f.Mert)).Should().ContainSingle().Which.Should().BeEquivalentTo(new { Amount = 120m, Note = "cihazdan", CreatedBy = f.MertId });
    }

    [Fact]
    public async Task After_leaving_a_device_still_sending_the_personal_header_just_works_in_the_personal_ledger()
    {
        var f = await Kayalar();
        var householdExpense = await Spend(f.Mert, 300, "Market", "Ortak market", "2026-09-15");
        f.Mert.UsePersonalLedger();
        await Spend(f.Mert, 40, "Food", "Kişisel çay", "2026-09-16");
        (await f.Mert.PostAsync("/api/households/leave", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The device keeps sending X-Ledger: personal; with no household that is simply where he works.
        var me = await f.Mert.GetFromJsonAsync<MeResponse>("/api/account/me", TestClient.Json);
        me!.Should().BeEquivalentTo(new { Household = (HouseholdSummary?)null, Ledger = "personal" });
        (await Month(f.Mert)).Select(e => e.Note).Should().Equal("Kişisel çay");
        (await f.Mert.DeleteAsync($"/api/expenses/{householdExpense.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "the household's expenses stay with the household");
        await Spend(f.Mert, 15, "Food", "Su", "2026-09-30");

        // Dropping the header changes nothing for someone without a household.
        f.Mert.UseDefaultLedger();
        (await f.Mert.GetFromJsonAsync<MeResponse>("/api/account/me", TestClient.Json))!.Ledger.Should().Be("personal");
        (await Month(f.Mert)).Select(e => e.Note).Should().BeEquivalentTo(["Kişisel çay", "Su"]);
        (await Month(f.Zeynep)).Select(e => e.Note).Should().Equal("Ortak market");
    }

    [Fact]
    public async Task The_header_value_is_matched_case_insensitively_and_trimmed()
    {
        var f = await Kayalar();
        f.Mert.DefaultRequestHeaders.Add("X-Ledger", "  Personal ");

        (await f.Mert.GetFromJsonAsync<MeResponse>("/api/account/me", TestClient.Json))!.Ledger.Should().Be("personal");
    }
}
