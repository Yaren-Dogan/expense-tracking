using System.Net;
using System.Net.Http.Json;
using ExpenseTracker.Api.Contracts;
using ExpenseTracker.Application.Features.Budgets;
using ExpenseTracker.Application.Features.Expenses;
using ExpenseTracker.Application.Features.Households;
using FluentAssertions;

namespace ExpenseTracker.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class HouseholdApiTests(ApiFactory factory)
{
    private static object Expense(decimal amount, string note, string spentOn = "2026-09-20") =>
        new { amount, category = "Market", note, spentOn };

    private async Task<(HttpClient Ayse, HttpClient Mehmet, HouseholdDto Household)> Couple()
    {
        var ayse = await factory.ForNewUserAsync("Ayşe");
        var mehmet = await factory.ForNewUserAsync("Mehmet");
        var household = await (await ayse.PostAsJsonAsync("/api/households", new { name = "Yılmaz Ailesi" })).Read<HouseholdDto>();
        (await mehmet.PostAsJsonAsync("/api/households/join", new { code = household.InviteCode })).EnsureSuccessStatusCode();
        return (ayse, mehmet, household);
    }

    private static Task<List<ExpenseDto>?> September(HttpClient client) =>
        client.GetFromJsonAsync<List<ExpenseDto>>("/api/expenses?month=2026-09", TestClient.Json);

    [Fact]
    public async Task Preview_shows_who_is_in_the_household_before_joining()
    {
        var ayse = await factory.ForNewUserAsync("Ayşe");
        var household = await (await ayse.PostAsJsonAsync("/api/households", new { name = "Yılmaz Ailesi" })).Read<HouseholdDto>();
        var mehmet = await factory.ForNewUserAsync("Mehmet");

        var preview = await mehmet.GetFromJsonAsync<InvitePreviewDto>(
            $"/api/households/invites/{household.InviteCode.ToLowerInvariant()}", TestClient.Json);

        preview.Should().BeEquivalentTo(new InvitePreviewDto("Yılmaz Ailesi", ["Ayşe"]));
        (await mehmet.GetFromJsonAsync<MeResponse>("/api/account/me", TestClient.Json))!.Household.Should().BeNull("previewing does not join");
    }

    [Fact]
    public async Task Members_share_expenses_and_budget_but_outsiders_see_nothing()
    {
        var (ayse, mehmet, _) = await Couple();
        var outsider = await factory.ForNewUserAsync();

        await ayse.PostExpense(Expense(300, "Migros"));
        await mehmet.PostExpense(Expense(200, "Pazar"));
        (await mehmet.PutAsJsonAsync("/api/budgets/2026-09", new { amount = 10000 })).EnsureSuccessStatusCode();

        var seenByAyse = await September(ayse);
        seenByAyse.Should().HaveCount(2);
        seenByAyse!.Select(e => e.CreatedByName).Should().BeEquivalentTo(["Ayşe", "Mehmet"]);
        (await ayse.GetFromJsonAsync<BudgetDto>("/api/budgets/2026-09", TestClient.Json))!.Amount.Should().Be(10000);

        (await September(outsider)).Should().BeEmpty();
        (await outsider.GetAsync("/api/budgets/2026-09")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Members_can_only_delete_their_own_expenses()
    {
        var (ayse, mehmet, _) = await Couple();
        var hers = await (await ayse.PostExpense(Expense(300, "Migros"))).Read<ExpenseDto>();

        (await mehmet.DeleteAsync($"/api/expenses/{hers.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ayse.DeleteAsync($"/api/expenses/{hers.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Personal_history_stays_personal_and_household_data_stays_in_the_household()
    {
        var ayse = await factory.ForNewUserAsync("Ayşe");
        var mehmet = await factory.ForNewUserAsync("Mehmet");
        await mehmet.PostExpense(Expense(999, "Kişisel, katılmadan önce"));

        var household = await (await ayse.PostAsJsonAsync("/api/households", new { name = "Ev" })).Read<HouseholdDto>();
        await mehmet.PostAsJsonAsync("/api/households/join", new { code = household.InviteCode });
        await mehmet.PostExpense(Expense(50, "Ortak ekmek"));

        (await September(ayse))!.Select(e => e.Note).Should().Equal("Ortak ekmek");

        (await mehmet.PostAsync("/api/households/leave", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await September(mehmet))!.Select(e => e.Note).Should().Equal("Kişisel, katılmadan önce");
        (await September(ayse))!.Select(e => e.Note).Should().Equal("Ortak ekmek");
    }

    [Fact]
    public async Task A_user_belongs_to_one_household_at_a_time()
    {
        var (_, mehmet, _) = await Couple();
        var other = await factory.ForNewUserAsync();
        var otherHousehold = await (await other.PostAsJsonAsync("/api/households", new { name = "Başka" })).Read<HouseholdDto>();

        (await mehmet.PostAsJsonAsync("/api/households/join", new { code = otherHousehold.InviteCode }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await mehmet.PostAsJsonAsync("/api/households", new { name = "Yeni" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Only_the_owner_manages_members_and_the_invite_code()
    {
        var (ayse, mehmet, household) = await Couple();
        var ayseId = household.Members.Single(m => m.IsMe).UserId;
        var mehmetId = (await mehmet.GetFromJsonAsync<MeResponse>("/api/account/me", TestClient.Json))!.Id;

        (await mehmet.PostAsync("/api/households/invite-code", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await mehmet.DeleteAsync($"/api/households/members/{ayseId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var renewed = await (await ayse.PostAsync("/api/households/invite-code", null)).Read<HouseholdDto>();
        renewed.InviteCode.Should().NotBe(household.InviteCode);
        var stranger = await factory.ForNewUserAsync();
        (await stranger.GetAsync($"/api/households/invites/{household.InviteCode}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var afterRemoval = await (await ayse.DeleteAsync($"/api/households/members/{mehmetId}")).Read<HouseholdDto>();
        afterRemoval.Members.Should().ContainSingle();
        (await mehmet.GetAsync("/api/households/current")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task A_removed_member_cannot_rejoin_with_the_code_they_already_know()
    {
        var (ayse, mehmet, household) = await Couple();
        var mehmetId = (await mehmet.GetFromJsonAsync<MeResponse>("/api/account/me", TestClient.Json))!.Id;

        var afterRemoval = await (await ayse.DeleteAsync($"/api/households/members/{mehmetId}")).Read<HouseholdDto>();
        afterRemoval.InviteCode.Should().NotBe(household.InviteCode, "every member has seen the old code");
        (await ayse.PostAsJsonAsync("/api/expenses", Expense(300, "ayrılıktan sonra"))).EnsureSuccessStatusCode();

        (await mehmet.GetAsync($"/api/households/invites/{household.InviteCode}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await mehmet.PostAsJsonAsync("/api/households/join", new { code = household.InviteCode })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await September(mehmet))!.Should().NotContain(e => e.Note == "ayrılıktan sonra");
    }

    [Fact]
    public async Task A_household_everyone_has_left_cannot_be_joined_with_its_old_code()
    {
        var (ayse, mehmet, household) = await Couple();
        (await ayse.PostAsJsonAsync("/api/expenses", Expense(500, "aile geçmişi"))).EnsureSuccessStatusCode();
        (await mehmet.PostAsync("/api/households/leave", null)).EnsureSuccessStatusCode();
        (await ayse.PostAsync("/api/households/leave", null)).EnsureSuccessStatusCode();

        var stranger = await factory.ForNewUserAsync();
        (await stranger.GetAsync($"/api/households/invites/{household.InviteCode}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.PostAsJsonAsync("/api/households/join", new { code = household.InviteCode })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await September(stranger))!.Should().BeEmpty();
    }

    [Fact]
    public async Task Invalid_invite_codes_are_rejected()
    {
        var client = await factory.ForNewUserAsync();

        (await client.GetAsync("/api/households/invites/ABC")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/households/join", new { code = "ZZZZZZZZ" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
