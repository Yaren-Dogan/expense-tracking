namespace ExpenseTracker.Infrastructure.Ai;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Must match the <c>vector(n)</c> column; changing it needs a migration.</summary>
    public const int EmbeddingDimensions = 1024;

    public string Endpoint { get; set; } = "http://localhost:11434";
    public string ChatModel { get; set; } = "qwen2.5:3b";
    public string EmbeddingModel { get; set; } = "bge-m3";

    /// <summary>CPU inference of a 3B model can take tens of seconds for an agent turn with several tool calls.</summary>
    public int RequestTimeoutSeconds { get; set; } = 180;

    public int IndexIntervalSeconds { get; set; } = 10;
    public int IndexBatchSize { get; set; } = 32;

    /// <summary>Search results below this cosine similarity are treated as unrelated.</summary>
    public double MinSimilarity { get; set; } = 0.5;
}
