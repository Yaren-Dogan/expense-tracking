using System.Text;
using Microsoft.Extensions.AI;

namespace ExpenseTracker.Api.IntegrationTests;

/// <summary>Scripted model shared by the test collection. Tests run serially within the collection, so each test scripts its own replies.</summary>
public sealed class ProgrammableChatClient : IChatClient
{
    private readonly Queue<Func<ChatResponse>> _script = new();

    public List<List<ChatMessage>> Requests { get; } = [];

    public void Reset()
    {
        _script.Clear();
        Requests.Clear();
    }

    public void Reply(string text) => _script.Enqueue(() => new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));

    public void Fail(Exception exception) => _script.Enqueue(() => throw exception);

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Requests.Add(messages.ToList());
        if (_script.Count == 0)
            throw new InvalidOperationException("The test did not script a model reply.");
        return Task.FromResult(_script.Dequeue()());
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}

/// <summary>
/// Deterministic bag-of-words embeddings: texts sharing word stems get similar vectors. Not semantic like bge-m3,
/// but enough to test the real pgvector pipeline (indexing, cosine ranking, thresholds, user isolation) without a model.
/// </summary>
public sealed class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public const int Dimensions = 1024;

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(values.Select(v => new Embedding<float>(Embed(v)))));

    private static float[] Embed(string text)
    {
        var vector = new float[Dimensions];
        var words = text.ToLowerInvariant().Split([' ', ':', ',', '.', '\'', '-'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            // First four letters as a crude stem: "kahve", "kahveye" and "kahvesi" land in the same bucket.
            var stem = word.Length > 4 ? word[..4] : word;
            vector[Fnv(stem) % Dimensions] += 1f;
        }

        var norm = MathF.Sqrt(vector.Sum(x => x * x));
        if (norm == 0) vector[0] = 1f;
        else for (var i = 0; i < vector.Length; i++) vector[i] /= norm;
        return vector;
    }

    private static int Fnv(string s)
    {
        var hash = 2166136261u;
        foreach (var b in Encoding.UTF8.GetBytes(s)) hash = (hash ^ b) * 16777619u;
        return (int)(hash & 0x7fffffff);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
