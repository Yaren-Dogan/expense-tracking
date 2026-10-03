namespace ExpenseTracker.Infrastructure.Ai;

public enum AiProvider
{
    /// <summary>Local models through Ollama's native API.</summary>
    Ollama,

    /// <summary>Any OpenAI-compatible API: Google Gemini (free tier), OpenAI, Groq, or Ollama's /v1 endpoint.</summary>
    OpenAICompatible
}

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public AiProvider Provider { get; set; } = AiProvider.Ollama;

    /// <summary>Required for hosted providers. Comes from the environment (.env), never from a committed file.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Must match the <c>vector(n)</c> column; changing it needs a migration.</summary>
    public const int EmbeddingDimensions = 1024;

    public string Endpoint { get; set; } = "http://localhost:11434";
    public string ChatModel { get; set; } = "qwen2.5:3b";
    public string EmbeddingModel { get; set; } = "bge-m3";

    /// <summary>CPU inference of a 3B model can take tens of seconds for an agent turn with several tool calls.</summary>
    public int RequestTimeoutSeconds { get; set; } = 180;

    public int IndexIntervalSeconds { get; set; } = 10;
    public int IndexBatchSize { get; set; } = 32;

    /// <summary>
    /// Search results below this cosine similarity are treated as unrelated. Leave unset to use the measured default
    /// for the embedding model (<see cref="EffectiveMinSimilarity"/>).
    /// </summary>
    public double? MinSimilarity { get; set; }

    /// <summary>
    /// Similarity scales differ per model, so one threshold does not fit all. Measured on Turkish expense notes:
    /// bge-m3 scores related pairs 0.54–0.72 and unrelated ones 0.34–0.52; gemini-embedding-001 (truncated to 1024)
    /// scores related pairs 0.70–0.80 and unrelated ones 0.48–0.63, so 0.5 filtered nothing there.
    /// </summary>
    public double EffectiveMinSimilarity => MinSimilarity ?? (EmbeddingModel.StartsWith("gemini-embedding", StringComparison.OrdinalIgnoreCase) ? 0.68 : 0.5);
}
