using System.Net;
using System.Net.Http.Json;
using ExpenseTracker.Api.Contracts;
using ExpenseTracker.Application.Features.Expenses;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AccountApiTests(ApiFactory factory)
{
    [Fact]
    public async Task Register_signs_the_user_in_and_logout_ends_the_session()
    {
        var client = await factory.ForNewUserAsync("Ayşe");

        var me = await client.GetFromJsonAsync<MeResponse>("/api/account/me", TestClient.Json);
        me!.DisplayName.Should().Be("Ayşe");
        me.Household.Should().BeNull();

        (await client.PostAsync("/api/account/logout", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync("/api/account/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_restores_access_to_the_same_data()
    {
        var first = await factory.ForNewUserAsync();
        await first.PostExpense(new { amount = 42, category = "Food", note = "simit", spentOn = "2026-09-10" });

        var second = factory.CreateClient();
        (await second.PostAsJsonAsync("/api/account/login", new { email = first.Email(), password = TestClient.Password }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await second.GetFromJsonAsync<List<ExpenseDto>>("/api/expenses?month=2026-09", TestClient.Json))
            .Should().ContainSingle().Which.Note.Should().Be("simit");
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_get_the_same_answer()
    {
        var user = await factory.ForNewUserAsync();
        var client = factory.CreateClient();

        var wrong = await client.PostAsJsonAsync("/api/account/login", new { email = user.Email(), password = "yanlis1234" });
        var unknown = await client.PostAsJsonAsync("/api/account/login", new { email = "nobody@test.local", password = "yanlis1234" });

        wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknown.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await wrong.Read<ProblemDetails>()).Detail.Should().Be((await unknown.Read<ProblemDetails>()).Detail);
    }

    [Fact]
    public async Task Weak_passwords_and_duplicate_emails_are_rejected_per_field()
    {
        var existing = await factory.ForNewUserAsync();
        var client = factory.CreateClient();

        var weak = await client.PostAsJsonAsync("/api/account/register", new { email = "x@test.local", password = "short", displayName = "X" });
        var duplicate = await client.PostAsJsonAsync("/api/account/register",
            new { email = existing.Email(), password = TestClient.Password, displayName = "X" });

        (await weak.Read<ValidationProblemDetails>()).Errors.Should().ContainKey("password");
        (await duplicate.Read<ValidationProblemDetails>()).Errors.Should().ContainKey("email");
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account()
    {
        var user = await factory.ForNewUserAsync();
        var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
            await client.PostAsJsonAsync("/api/account/login", new { email = user.Email(), password = "yanlis1234" });
        var correct = await client.PostAsJsonAsync("/api/account/login", new { email = user.Email(), password = TestClient.Password });

        correct.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await correct.Read<ProblemDetails>()).Detail.Should().Contain("5 dakika");
    }

    [Fact]
    public async Task Device_data_moves_into_the_new_account()
    {
        // Simulate data written before accounts existed: rows owned by a random device id.
        var deviceId = Guid.NewGuid();
        await factory.SeedDeviceExpenseAsync(deviceId, 120m, "2026-09-12");

        var client = await factory.ForNewUserAsync(deviceUserId: deviceId);

        (await client.GetFromJsonAsync<List<ExpenseDto>>("/api/expenses?month=2026-09", TestClient.Json))
            .Should().ContainSingle().Which.Amount.Should().Be(120m);
    }

    [Fact]
    public async Task Another_users_id_cannot_be_used_to_claim_their_data()
    {
        var victim = await factory.ForNewUserAsync();
        await victim.PostExpense(new { amount = 500, category = "Bills", note = "kira", spentOn = "2026-09-01" });
        var victimId = (await victim.GetFromJsonAsync<MeResponse>("/api/account/me", TestClient.Json))!.Id;

        var attacker = await factory.ForNewUserAsync(deviceUserId: victimId);

        (await attacker.GetFromJsonAsync<List<ExpenseDto>>("/api/expenses?month=2026-09", TestClient.Json)).Should().BeEmpty();
        (await victim.GetFromJsonAsync<List<ExpenseDto>>("/api/expenses?month=2026-09", TestClient.Json)).Should().ContainSingle();
    }
}
