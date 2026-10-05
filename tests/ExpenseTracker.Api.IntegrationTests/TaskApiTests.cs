using System.Net;
using System.Net.Http.Json;
using ExpenseTracker.Api.Contracts;
using ExpenseTracker.Application.Features.Expenses;
using ExpenseTracker.Application.Features.Households;
using ExpenseTracker.Application.Features.Notifications;
using ExpenseTracker.Application.Features.Tasks;
using ExpenseTracker.Domain.Notifications;
using FluentAssertions;

namespace ExpenseTracker.Api.IntegrationTests;

// The test clock is pinned to 2026-09-30.
[Collection(ApiCollection.Name)]
public class TaskApiTests(ApiFactory factory)
{
    private sealed record Family(HttpClient Yaren, HttpClient Ahmet, HouseholdDto Household, Guid YarenId, Guid AhmetId);

    private async Task<Family> Doganlar()
    {
        var yaren = await factory.ForNewUserAsync("Yaren");
        var ahmet = await factory.ForNewUserAsync("Ahmet");
        var household = await (await yaren.PostAsJsonAsync("/api/households", new { name = "Doğan ailesi" })).Read<HouseholdDto>();
        var joined = await (await ahmet.PostAsJsonAsync("/api/households/join", new { code = household.InviteCode })).Read<HouseholdDto>();
        return new Family(yaren, ahmet, joined,
            joined.Members.Single(m => m.DisplayName == "Yaren").UserId,
            joined.Members.Single(m => m.DisplayName == "Ahmet").UserId);
    }

    private static async Task<TaskDto> CreateTask(HttpClient client, string title, Guid assignee, string dueOn, string recurrence = "None")
    {
        var response = await client.PostAsJsonAsync("/api/tasks", new { title, assigneeId = assignee, dueOn, recurrence });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await response.Read<TaskDto>();
    }

    private static Task<TaskListDto?> Tasks(HttpClient client) => client.GetFromJsonAsync<TaskListDto>("/api/tasks", TestClient.Json);

    private static Task<NotificationListDto?> Notifications(HttpClient client) =>
        client.GetFromJsonAsync<NotificationListDto>("/api/notifications", TestClient.Json);

    [Fact]
    public async Task Assigning_a_task_notifies_the_assignee_and_everyone_in_the_household_sees_it()
    {
        var f = await Doganlar();

        var task = await CreateTask(f.Yaren, "İnternet faturası", f.AhmetId, "2026-10-12");

        task.Should().BeEquivalentTo(new { AssigneeName = "Ahmet", CreatedByName = "Yaren", Category = Domain.Enums.ExpenseCategory.Bills, CanComplete = true, CanDelete = true });
        (await Tasks(f.Ahmet))!.Open.Should().ContainSingle(t => t.Id == task.Id && t.CanComplete && !t.CanDelete);

        var inbox = await Notifications(f.Ahmet);
        inbox!.UnreadCount.Should().Be(1);
        inbox.Items.Single().Should().BeEquivalentTo(new
        {
            Kind = NotificationKind.TaskAssigned,
            SubjectId = task.Id,
            Message = "Yaren sana bir görev verdi: İnternet faturası · son gün 12 Ekim",
            IsRead = false
        });
        (await Notifications(f.Yaren))!.Items.Should().BeEmpty("giving yourself nothing to do needs no notice");

        (await f.Ahmet.PostAsync("/api/notifications/read", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var read = await Notifications(f.Ahmet);
        read!.UnreadCount.Should().Be(0);
        read.Items.Single().IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task Tasks_can_only_go_to_people_in_the_household_and_stay_inside_it()
    {
        var f = await Doganlar();
        var outsider = await factory.ForNewUserAsync("Komşu");
        var outsiderId = (await outsider.GetFromJsonAsync<MeResponse>("/api/account/me", TestClient.Json))!.Id;

        var response = await f.Yaren.PostAsJsonAsync("/api/tasks", new { title = "Su faturası", assigneeId = outsiderId, dueOn = "2026-10-10" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("assigneeId");

        var task = await CreateTask(f.Yaren, "Su faturası", f.AhmetId, "2026-10-10");
        (await Tasks(outsider))!.Open.Should().BeEmpty();
        (await outsider.PostAsJsonAsync($"/api/tasks/{task.Id}/complete", new { })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await outsider.DeleteAsync($"/api/tasks/{task.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Paying_a_monthly_bill_records_the_expense_opens_next_month_and_tells_the_creator()
    {
        var f = await Doganlar();
        var task = await CreateTask(f.Yaren, "Elektrik faturası", f.AhmetId, "2026-09-25", "Monthly");

        var response = await f.Ahmet.PostAsJsonAsync($"/api/tasks/{task.Id}/complete", new { amount = 812.40m });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Read<CompleteTaskResult>();

        result.Completed.Should().BeEquivalentTo(new { PaidAmount = 812.40m, CompletedByName = "Ahmet", CanComplete = false });
        result.Next!.DueOn.Should().Be(new DateOnly(2026, 10, 25));
        result.Next.AssigneeId.Should().Be(f.AhmetId);

        var september = await f.Yaren.GetFromJsonAsync<List<ExpenseDto>>("/api/expenses?month=2026-09", TestClient.Json);
        september.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Amount = 812.40m,
            Category = Domain.Enums.ExpenseCategory.Bills,
            Note = "Elektrik faturası",
            SpentOn = new DateOnly(2026, 9, 30),
            CreatedByName = "Ahmet"
        }, "the payment lands in the household ledger on the day it was paid");

        var list = await Tasks(f.Yaren);
        list!.Open.Should().ContainSingle(t => t.Id == result.Next.Id);
        list.Done.Should().ContainSingle(t => t.Id == task.Id);

        (await Notifications(f.Yaren))!.Items.Should().ContainSingle(n =>
            n.Kind == NotificationKind.TaskCompleted && n.Message == "Ahmet tamamladı: Elektrik faturası · ₺812,40 ödendi");

        var again = await f.Yaren.PostAsJsonAsync($"/api/tasks/{task.Id}/complete", new { });
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Completing_without_an_amount_adds_no_expense_and_validates_the_amount()
    {
        var f = await Doganlar();
        var task = await CreateTask(f.Yaren, "Aidat", f.AhmetId, "2026-10-05");

        (await f.Ahmet.PostAsJsonAsync($"/api/tasks/{task.Id}/complete", new { amount = -5 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await f.Ahmet.PostAsync($"/api/tasks/{task.Id}/complete", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await f.Yaren.GetFromJsonAsync<List<ExpenseDto>>("/api/expenses?month=2026-09", TestClient.Json)).Should().BeEmpty();
    }

    [Fact]
    public async Task Only_the_creator_or_the_owner_can_delete_and_outsiders_to_a_task_cannot_complete_it()
    {
        var f = await Doganlar();
        var can = await factory.ForNewUserAsync("Can");
        await can.PostAsJsonAsync("/api/households/join", new { code = f.Household.InviteCode });

        var byAhmet = await CreateTask(f.Ahmet, "Market alışverişi", f.AhmetId, "2026-10-01");

        (await can.PostAsJsonAsync($"/api/tasks/{byAhmet.Id}/complete", new { })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await can.DeleteAsync($"/api/tasks/{byAhmet.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await f.Yaren.DeleteAsync($"/api/tasks/{byAhmet.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent, "Yaren owns the household");
        (await Tasks(f.Ahmet))!.Open.Should().BeEmpty();
    }

    [Fact]
    public async Task When_the_giver_ticks_off_a_task_the_payment_stays_with_the_assignee()
    {
        var f = await Doganlar();
        var task = await CreateTask(f.Yaren, "Market alışverişi", f.AhmetId, "2026-10-05");

        var result = await (await f.Yaren.PostAsJsonAsync($"/api/tasks/{task.Id}/complete", new { amount = 5000 })).Read<CompleteTaskResult>();

        result.Completed.Should().BeEquivalentTo(new { AssigneeName = "Ahmet", CompletedByName = "Yaren" }, "the task stays Ahmet's");
        var expense = (await f.Ahmet.GetFromJsonAsync<List<ExpenseDto>>("/api/expenses?month=2026-09", TestClient.Json))!.Single();
        expense.CreatedBy.Should().Be(f.AhmetId, "it was Ahmet's bill");
        (await f.Yaren.DeleteAsync($"/api/expenses/{expense.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await f.Ahmet.DeleteAsync($"/api/expenses/{expense.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Notifications(f.Ahmet))!.Items.Should().Contain(n => n.Kind == NotificationKind.TaskCompleted && n.Message.StartsWith("Yaren tamamladı"));
    }

    [Fact]
    public async Task The_assignee_can_clear_a_finished_task_but_not_an_open_one()
    {
        var f = await Doganlar();
        var can = await factory.ForNewUserAsync("Can");
        await can.PostAsJsonAsync("/api/households/join", new { code = f.Household.InviteCode });
        var open = await CreateTask(f.Yaren, "Açık görev", f.AhmetId, "2026-10-05");
        var done = await CreateTask(f.Yaren, "Biten görev", f.AhmetId, "2026-10-05");
        (await f.Ahmet.PostAsync($"/api/tasks/{done.Id}/complete", null)).EnsureSuccessStatusCode();

        var list = await Tasks(f.Ahmet);
        list!.Open.Single(t => t.Id == open.Id).CanDelete.Should().BeFalse();
        list.Done.Single(t => t.Id == done.Id).CanDelete.Should().BeTrue();

        (await f.Ahmet.DeleteAsync($"/api/tasks/{open.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await can.DeleteAsync($"/api/tasks/{done.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "Can neither gave nor did it");
        (await f.Ahmet.DeleteAsync($"/api/tasks/{done.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Reminders_come_once_when_due_soon_and_once_when_overdue()
    {
        var f = await Doganlar();
        var soon = await CreateTask(f.Yaren, "Doğalgaz faturası", f.AhmetId, "2026-10-02");
        var late = await CreateTask(f.Yaren, "Kira", f.AhmetId, "2026-09-28");
        var later = await CreateTask(f.Yaren, "Sigorta", f.AhmetId, "2026-10-20");

        await factory.RunTaskRemindersAsync();
        await factory.RunTaskRemindersAsync();

        var mine = (await Notifications(f.Ahmet))!.Items;
        mine.Where(n => n.SubjectId == soon.Id && n.Kind == NotificationKind.TaskDueSoon).Should().ContainSingle()
            .Which.Message.Should().Be("Doğalgaz faturası için son gün 2 gün sonra (2 Ekim)");
        mine.Where(n => n.SubjectId == late.Id && n.Kind == NotificationKind.TaskOverdue).Should().ContainSingle()
            .Which.Message.Should().Be("Kira gecikti: son gün 28 Eylül idi");
        mine.Should().NotContain(n => n.SubjectId == later.Id && n.Kind != NotificationKind.TaskAssigned, "20 Ekim is more than 3 days away");
    }

    [Fact]
    public async Task Leaving_the_household_hands_open_tasks_to_the_owner()
    {
        var f = await Doganlar();
        var task = await CreateTask(f.Yaren, "İnternet faturası", f.AhmetId, "2026-10-12");

        (await f.Ahmet.PostAsync("/api/households/leave", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var open = (await Tasks(f.Yaren))!.Open.Single();
        open.Id.Should().Be(task.Id);
        open.AssigneeId.Should().Be(f.YarenId);
        (await Notifications(f.Yaren))!.Items.Should().ContainSingle(n =>
            n.SubjectId == task.Id && n.Message.StartsWith("Ahmet haneden ayrıldı, görevi sana geçti: İnternet faturası"));
        (await Tasks(f.Ahmet))!.Open.Should().BeEmpty("back in his personal ledger");
    }

    [Fact]
    public async Task Without_a_household_tasks_are_personal_reminders()
    {
        var solo = await factory.ForNewUserAsync("Tek");
        var me = (await solo.GetFromJsonAsync<MeResponse>("/api/account/me", TestClient.Json))!;

        var task = await CreateTask(solo, "Telefon faturası", me.Id, "2026-10-01");

        task.CanComplete.Should().BeTrue();
        (await Notifications(solo))!.Items.Should().BeEmpty();
        await factory.RunTaskRemindersAsync();
        (await Notifications(solo))!.Items.Should().ContainSingle(n => n.Kind == NotificationKind.TaskDueSoon);
    }
}
