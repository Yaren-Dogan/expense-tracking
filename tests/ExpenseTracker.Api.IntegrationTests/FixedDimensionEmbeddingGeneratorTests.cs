using ExpenseTracker.Infrastructure.Ai;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace ExpenseTracker.Api.IntegrationTests;

public class FixedDimensionEmbeddingGeneratorTests
{
    private sealed class StubGenerator(params float[] vector) : IEmbeddingGenerator<string, Embedding<float>>
    {
        public EmbeddingGenerationOptions? LastOptions { get; private set; }

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(values.Select(_ => new Embedding<float>(vector))));
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    [Fact]
    public async Task Longer_vectors_are_truncated_and_renormalised()
    {
        var stub = new StubGenerator(3f, 4f, 100f, 100f);
        var generator = new FixedDimensionEmbeddingGenerator(stub, 2);

        var vector = (await generator.GenerateAsync(["x"]))[0].Vector.ToArray();

        vector.Should().Equal(0.6f, 0.8f);
        stub.LastOptions!.Dimensions.Should().Be(2, "providers that support it are asked for the right size");
    }

    [Fact]
    public async Task Vectors_of_the_right_size_pass_through()
    {
        var generator = new FixedDimensionEmbeddingGenerator(new StubGenerator(0.5f, 0.5f), 2);

        (await generator.GenerateAsync(["x"]))[0].Vector.ToArray().Should().Equal(0.5f, 0.5f);
    }

    [Fact]
    public async Task Shorter_vectors_cannot_fill_the_index()
    {
        var generator = new FixedDimensionEmbeddingGenerator(new StubGenerator(1f), 2);

        var act = () => generator.GenerateAsync(["x"]);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
