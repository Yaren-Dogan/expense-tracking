using Microsoft.Extensions.AI;

namespace ExpenseTracker.Infrastructure.Ai;

/// <summary>
/// Makes every provider fit the fixed <c>vector(n)</c> column. Modern embedding models (Gemini, OpenAI text-embedding-3)
/// are trained so their vectors can be truncated (Matryoshka representation learning): the first n dimensions,
/// re-normalised, keep most of the meaning. Providers that honour the requested size are passed through untouched.
/// </summary>
public sealed class FixedDimensionEmbeddingGenerator(IEmbeddingGenerator<string, Embedding<float>> inner, int dimensions)
    : DelegatingEmbeddingGenerator<string, Embedding<float>>(inner)
{
    public override async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        options = options?.Clone() ?? new EmbeddingGenerationOptions();
        options.Dimensions ??= dimensions;

        var generated = await base.GenerateAsync(values, options, cancellationToken);
        var adjusted = new GeneratedEmbeddings<Embedding<float>>(generated.Select(Fit)) { Usage = generated.Usage };
        return adjusted;
    }

    public Embedding<float> Fit(Embedding<float> embedding)
    {
        var vector = embedding.Vector;
        if (vector.Length == dimensions)
            return embedding;
        if (vector.Length < dimensions)
            throw new InvalidOperationException(
                $"The embedding model returns {vector.Length} dimensions but the index needs {dimensions}. Use a larger model or migrate the column.");

        var truncated = vector[..dimensions].ToArray();
        var norm = MathF.Sqrt(truncated.Sum(x => x * x));
        if (norm > 0)
            for (var i = 0; i < truncated.Length; i++) truncated[i] /= norm;

        return new Embedding<float>(truncated) { ModelId = embedding.ModelId, CreatedAt = embedding.CreatedAt };
    }
}
