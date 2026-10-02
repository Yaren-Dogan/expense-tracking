using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace ExpenseTracker.Infrastructure.Ai;

/// <summary>
/// Minimal client for the OpenAI <c>/embeddings</c> endpoint. Written by hand because the OpenAI SDK path throws a
/// NullReferenceException when a provider omits the optional <c>usage</c> field, which Gemini's OpenAI-compatible API does.
/// Only <c>data[].embedding</c> is read, so any compatible provider works.
/// </summary>
public sealed class OpenAICompatibleEmbeddingGenerator(HttpClient http, string model) : IEmbeddingGenerator<string, Embedding<float>>
{
    private sealed record Request(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] IReadOnlyList<string> Input,
        [property: JsonPropertyName("encoding_format")] string EncodingFormat = "float");

    private sealed record Response([property: JsonPropertyName("data")] List<Item>? Data);

    private sealed record Item(
        [property: JsonPropertyName("index")] int Index,
        [property: JsonPropertyName("embedding")] float[] Embedding);

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var input = values.ToList();

        // Dimensions are not sent: support for the parameter varies between providers, and
        // FixedDimensionEmbeddingGenerator fits whatever size comes back.
        using var response = await http.PostAsJsonAsync("embeddings", new Request(model, input), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Embedding request failed with {(int)response.StatusCode}: {body[..Math.Min(body.Length, 300)]}",
                null, response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<Response>(cancellationToken);
        var items = payload?.Data ?? [];
        if (items.Count != input.Count)
            throw new HttpRequestException($"Embedding response had {items.Count} vectors for {input.Count} inputs.");

        return new GeneratedEmbeddings<Embedding<float>>(
            items.OrderBy(i => i.Index).Select(i => new Embedding<float>(i.Embedding) { ModelId = model }));
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() { }
}
