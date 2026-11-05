using System.Net;
using System.Net.Http.Json;
using ExpenseTracker.Application.Features.Budgets;
using ExpenseTracker.Application.Features.Households;
using ExpenseTracker.Application.Features.Insights.Plans;
using ExpenseTracker.Application.Features.Insights.Reviews;
using ExpenseTracker.Application.Features.Notifications;
using ExpenseTracker.Domain.Enums;
using ExpenseTracker.Domain.Insights;
using ExpenseTracker.Domain.Notifications;
using FluentAssertions;

namespace ExpenseTracker.Api.IntegrationTests;

// The test clock is pinned to 2026-09-30: August is the last finished month, October the first planned one.
[Collection(ApiCollection.Name)]
public sealed class InsightsApiTests : IDisposable
{
    private readonly ApiFactory _factory;

    public InsightsApiTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.Model.Reset();
    }

    public void Dispose() => _factory.Model.Reset();

    private static async Task Spend(HttpClient client, decimal amount, string category, string note, string spentOn) =>
        (await client.PostExpense(new { amount, category, note, spentOn })).EnsureSuccessStatusCode();

    /// <summary>July and August for a couple: transport jumps in August, which the review should notice.</summary>
    private async Task<(HttpClient Yaren, HttpClient Ahmet)> CoupleWithHistory()
    {
        var yaren = await _factory.ForNewUserAsync("Yaren");
        var ahmet = await _factory.ForNewUserAsync("Ahmet");
        var household = await (await yaren.PostAsJsonAsync("/api/households", new { name = "Doğan ailesi" })).Read<HouseholdDto>();
        (await ahmet.PostAsJsonAsync("/api/households/join", new { code = household.InviteCode })).EnsureSuccessStatusCode();
        (await yaren.PutAsJsonAsync("/api/budgets/2026-08", new { amount = 30000 })).EnsureSuccessStatusCode();

        await Spend(yaren, 15000, "Bills", "Kira", "2026-07-05");
        await Spend(yaren, 3000, "Market", "Migros", "2026-07-10");
        await Spend(ahmet, 900, "Transport", "Taksi", "2026-07-12");
        await Spend(ahmet, 2000, "Food", "Restoran", "2026-07-20");
        await Spend(yaren, 15000, "Bills", "Kira", "2026-08-05");
        await Spend(yaren, 3200, "Market", "Migros", "2026-08-10");
        await Spend(ahmet, 1440, "Transport", "Taksi", "2026-08-12");
        await Spend(ahmet, 1800, "Food", "Restoran", "2026-08-20");
        await Spend(ahmet, 600, "Entertainment", "Konser", "2026-08-25");
        return (yaren, ahmet);
    }

    private static string Step(string action, string category = "", string headline = "", string[]? highlights = null, string tip = "") =>
        System.Text.Json.JsonSerializer.Serialize(new { reason = "test", action, category, headline, highlights = highlights ?? [], tip });

    [Fact]
    public async Task The_review_agent_drills_down_then_chooses_code_computed_facts()
    {
        var (yaren, ahmet) = await CoupleWithHistory();
        _factory.Model.Reply(Step("DrillDown", category: "Transport"));
        _factory.Model.Reply(Step("Finish", headline: "Bütçenin içinde ama ulaşım hızlandı.",
            highlights: ["total", "cat-up:Transport", "drill:Transport", "uydurma-id"], tip: "Kısa yolları yürüyerek ya da toplu taşımayla gitmeyi dene."));

        var response = await yaren.PostAsync("/api/reviews/2026-08", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var review = await response.Read<MonthReviewDto>();

        review.Headline.Should().Be("Bütçenin içinde ama ulaşım hızlandı.");
        review.Highlights.Should().Equal(
            "Ağustos ayında toplam ₺22.040 harcandı; ₺30.000 bütçeyle ay ₺7.960 artıyla kapandı.",
            "Ulaşım harcaması bir önceki aya göre %60 arttı (₺900 → ₺1.440).",
            "Ulaşım: bu ay 1 harcama, ₺1.440; en büyükleri Taksi (₺1.440). Önceki ay 1 harcama, ₺900.");
        review.GeneratedBy.Should().NotBe(MonthReview.RulesAuthor);
        _factory.Model.Requests[1].Last().Text.Should().Contain("drill:Transport", "the drill-down result is fed back to the model");

        // Asking again returns the stored review without calling the model; the other member sees the same one.
        var again = await ahmet.PostAsync("/api/reviews/2026-08", null);
        (await again.Read<MonthReviewDto>()).Id.Should().Be(review.Id);
        (await ahmet.GetFromJsonAsync<MonthReviewDto>("/api/reviews/2026-08", TestClient.Json))!.Headline.Should().Be(review.Headline);
        _factory.Model.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task Numbers_in_the_models_own_words_or_a_failing_model_fall_back_to_the_rules()
    {
        var (yaren, _) = await CoupleWithHistory();
        _factory.Model.Reply(Step("Finish", headline: "Ayı 7.960 lira artıyla kapattın!", highlights: ["total"], tip: "Ayda 500 lira biriktir."));

        var invented = await (await yaren.PostAsync("/api/reviews/2026-08", null)).Read<MonthReviewDto>();
        invented.Headline.Should().Be("Parçalı bulutlu bir ay: bütçenin içinde kaldın.", "a model-written number could be wrong");
        invented.Tip.Should().NotContainAny("500");
        invented.Highlights.Should().ContainSingle().Which.Should().StartWith("Ağustos ayında toplam");

        _factory.Model.Reply(Step("Finish", headline: "Detaylar için www.ornek-site.com adresine gir", highlights: ["total"], tip: "Bize yaz: destek@ornek.com"));
        var linked = await (await yaren.PostAsync("/api/reviews/2026-08?refresh=true", null)).Read<MonthReviewDto>();
        linked.Headline.Should().Be("Parçalı bulutlu bir ay: bütçenin içinde kaldın.", "links planted through expense notes are dropped");
        linked.Tip.Should().NotContain("@");

        _factory.Model.Fail(new HttpRequestException("connection refused"));
        var refreshed = await (await yaren.PostAsync("/api/reviews/2026-08?refresh=true", null)).Read<MonthReviewDto>();
        refreshed.Id.Should().Be(invented.Id, "regenerating keeps the review's identity");
        refreshed.GeneratedBy.Should().Be(MonthReview.RulesAuthor);
        refreshed.Tip.Should().Be("Ulaşım harcamasına önümüzdeki ay haftalık bir sınır koymayı dene.");
    }

    [Fact]
    public async Task Only_finished_months_with_spending_can_be_reviewed_and_reviews_stay_in_their_ledger()
    {
        var (yaren, _) = await CoupleWithHistory();
        var outsider = await _factory.ForNewUserAsync();

        (await yaren.PostAsync("/api/reviews/2026-09", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest, "September is not over");
        (await yaren.PostAsync("/api/reviews/2026-05", null)).StatusCode.Should().Be(HttpStatusCode.NotFound, "nothing was spent in May");

        _factory.Model.Fail(new HttpRequestException("down"));
        (await yaren.PostAsync("/api/reviews/2026-08", null)).EnsureSuccessStatusCode();
        (await outsider.GetAsync("/api/reviews/2026-08")).StatusCode.Should().Be(HttpStatusCode.NoContent, "the outsider's own ledger has no review");
        _factory.Model.Requests.Should().HaveCount(1, "invalid requests never reach the model");
    }

    [Fact]
    public async Task The_monthly_job_reviews_last_month_once_and_tells_every_member()
    {
        var (yaren, ahmet) = await CoupleWithHistory();
        for (var i = 0; i < 600; i++)
            _factory.Model.Fail(new HttpRequestException("no model in this test"));

        await _factory.RunMonthReviewsAsync();
        await _factory.RunMonthReviewsAsync();

        var review = await yaren.GetFromJsonAsync<MonthReviewDto>("/api/reviews/2026-08", TestClient.Json);
        review!.GeneratedBy.Should().Be(MonthReview.RulesAuthor);
        foreach (var member in new[] { yaren, ahmet })
        {
            var inbox = await member.GetFromJsonAsync<NotificationListDto>("/api/notifications", TestClient.Json);
            inbox!.Items.Should().ContainSingle(n => n.Kind == NotificationKind.MonthReviewReady)
                .Which.Message.Should().Be($"Doğan ailesi · Ağustos 2026 değerlendirmesi hazır: {review.Headline}");
        }
    }

    private static string PlanStep(string action, string category = "", object[]? cuts = null, string advice = "") =>
        System.Text.Json.JsonSerializer.Serialize(new { reason = "test", action, category, cuts = cuts ?? [], advice });

    [Fact]
    public async Task The_planner_picks_flexible_categories_and_the_code_works_out_the_budget()
    {
        var (yaren, ahmet) = await CoupleWithHistory();
        _factory.Model.Reply(PlanStep("InspectCategory", category: "Food"));
        _factory.Model.Reply(PlanStep("Finish", cuts:
        [
            new { category = "Food", level = "Medium", reason = "Restoran yerine evde yemek kolay." },
            new { category = "Bills", level = "Strong", reason = "Faturaları kıs." },
            new { category = "Entertainment", level = "Strong", reason = "Konserler ertelenebilir." }
        ], advice: "Maaş günü tatil hesabına otomatik aktarım kur."));

        var response = await yaren.PostAsJsonAsync("/api/plans", new { goalAmount = 4000, months = 4, purpose = "Yaz tatili" });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var plan = await response.Read<BudgetPlanDto>();

        // Baseline: July 20.900 + August 22.040 over 2 months = 21.470 a month; 4.000 in 4 months = 1.000 a month.
        plan.BaselineMonthly.Should().Be(21470);
        plan.MonthlySaving.Should().Be(1000);
        plan.MonthlyBudget.Should().Be(20470);
        plan.StartMonth.Should().Be("2026-10");
        plan.EndMonth.Should().Be("2027-01");
        plan.Cuts.Select(c => c.Category).Should().Equal(ExpenseCategory.Food, ExpenseCategory.Entertainment);
        plan.Cuts.Should().NotContain(c => c.Category == ExpenseCategory.Bills, "bills are never cut");
        plan.Advice.Should().Be("Maaş günü tatil hesabına otomatik aktarım kur.");
        _factory.Model.Requests[1].Last().Text.Should().Contain("Restoran", "the inspect tool shows what the category is made of");
        _factory.Model.Requests[0][0].Text.Should().NotContain("Yaz tatili", "the user's words never enter the instructions");
        _factory.Model.Requests[0][1].Text.Should().Contain("<amac>Yaz tatili</amac>");
        (await yaren.GetAsync("/api/budgets/2026-10")).StatusCode.Should().Be(HttpStatusCode.NotFound, "nothing changes before applying");

        var applied = await ahmet.PostAsync($"/api/plans/{plan.Id}/apply", null);
        applied.StatusCode.Should().Be(HttpStatusCode.OK);
        (await applied.Read<BudgetPlanDto>()).AppliedByName.Should().Be("Ahmet");
        foreach (var month in new[] { "2026-10", "2026-11", "2026-12", "2027-01" })
            (await yaren.GetFromJsonAsync<BudgetDto>($"/api/budgets/{month}", TestClient.Json))!.Amount.Should().Be(20470);
        (await yaren.GetAsync("/api/budgets/2027-02")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await ahmet.PostAsync($"/api/plans/{plan.Id}/apply", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await yaren.GetFromJsonAsync<NotificationListDto>("/api/notifications", TestClient.Json))!.Items
            .Should().ContainSingle(n => n.Kind == NotificationKind.BudgetPlanApplied && n.Message.StartsWith("Ahmet birikim planını uyguladı"));
        (await yaren.GetFromJsonAsync<BudgetPlanDto>("/api/plans/latest", TestClient.Json))!.AppliedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Two_simultaneous_applies_of_one_plan_succeed_only_once()
    {
        // Every planned month already has a budget, so applying only updates rows: no unique index can catch the race,
        // only the applied_at concurrency token. A personal ledger also means no notification index to lean on.
        var me = await _factory.ForNewUserAsync();
        await Spend(me, 3000, "Market", "Migros", "2026-07-10");
        await Spend(me, 2000, "Food", "Restoran", "2026-08-20");
        foreach (var month in new[] { "2026-10", "2026-11" })
            (await me.PutAsJsonAsync($"/api/budgets/{month}", new { amount = 9999 })).EnsureSuccessStatusCode();

        _factory.Model.Fail(new HttpRequestException("down"));
        var plan = await (await me.PostAsJsonAsync("/api/plans", new { goalAmount = 400, months = 2 })).Read<BudgetPlanDto>();

        var responses = await Task.WhenAll(
            me.PostAsync($"/api/plans/{plan.Id}/apply", null),
            me.PostAsync($"/api/plans/{plan.Id}/apply", null));

        responses.Select(r => r.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        // Baseline (3.000 + 2.000) / 2 months = 2.500; 400 in 2 months = 200 a month; 2.500 − 200 = 2.300.
        foreach (var month in new[] { "2026-10", "2026-11" })
            (await me.GetFromJsonAsync<BudgetDto>($"/api/budgets/{month}", TestClient.Json))!.Amount.Should().Be(2300);
    }

    [Fact]
    public async Task Without_a_model_the_rules_plan_and_too_big_goals_are_stretched_or_refused()
    {
        var (yaren, _) = await CoupleWithHistory();
        var outsider = await _factory.ForNewUserAsync();

        _factory.Model.Fail(new HttpRequestException("down"));
        var stretched = await (await yaren.PostAsJsonAsync("/api/plans", new { goalAmount = 20000, months = 2 })).Read<BudgetPlanDto>();
        stretched.GeneratedBy.Should().Be(MonthReview.RulesAuthor);
        stretched.IsExtended.Should().BeTrue();
        stretched.Months.Should().BeGreaterThan(2);
        stretched.Cuts.Should().NotContain(c => c.Category == ExpenseCategory.Bills);

        _factory.Model.Fail(new HttpRequestException("down"));
        (await yaren.PostAsJsonAsync("/api/plans", new { goalAmount = 1_000_000, months = 2 })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await yaren.PostAsJsonAsync("/api/plans", new { goalAmount = 1000, months = 0 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await outsider.PostAsJsonAsync("/api/plans", new { goalAmount = 1000, months = 2 })).StatusCode
            .Should().Be(HttpStatusCode.Conflict, "an empty ledger has no history to plan from");
        (await outsider.PostAsync($"/api/plans/{stretched.Id}/apply", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await outsider.GetAsync("/api/plans/latest")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
