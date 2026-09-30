using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExpenseTracker.Api.IntegrationTests;

/// <summary>Each test gets a fresh user id, so tests are isolated without truncating tables.</summary>
public static class TestClient
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static HttpClient ForNewUser(this ApiFactory factory) => factory.ForUser(Guid.NewGuid());

    public static HttpClient ForUser(this ApiFactory factory, Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        return client;
    }

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
