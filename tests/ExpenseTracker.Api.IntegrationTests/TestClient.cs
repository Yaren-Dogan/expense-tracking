using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExpenseTracker.Api.IntegrationTests;

/// <summary>Each test registers a fresh account, so tests are isolated without truncating tables.</summary>
public static class TestClient
{
    public const string Password = "Parola123";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>A client signed in as a newly registered user; the auth cookie is kept by the client's cookie handler.</summary>
    public static async Task<HttpClient> ForNewUserAsync(this ApiFactory factory, string? displayName = null, Guid? deviceUserId = null)
    {
        var client = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@test.local";
        var response = await client.PostAsJsonAsync("/api/account/register",
            new { email, password = Password, displayName = displayName ?? "Test", deviceUserId }, Json);
        response.EnsureSuccessStatusCode();
        Emails.AddOrUpdate(client, email);
        return client;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<HttpClient, string> Emails = new();

    public static string Email(this HttpClient client) => Emails.TryGetValue(client, out var email) ? email : throw new InvalidOperationException();

    public static async Task<T> Read<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    public static Task<HttpResponseMessage> PostExpense(
        this HttpClient client, object body, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/expenses")
        {
            Content = JsonContent.Create(body, options: Json)
        };
        if (idempotencyKey is not null)
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        return client.SendAsync(request);
    }
}
