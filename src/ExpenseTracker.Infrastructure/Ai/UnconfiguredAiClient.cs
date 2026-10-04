using ExpenseTracker.Application.Common.Ai;
using ExpenseTracker.Application.Common.Exceptions;
using Microsoft.Extensions.AI;

namespace ExpenseTracker.Infrastructure.Ai;

/// <summary>
/// Stands in when a hosted provider has no API key, so the rest of the app still starts and works;
/// AI endpoints answer 503 with a message that says what to configure.
/// </summary>
internal sealed class UnconfiguredAiClient : IChatClient, IEmbeddingGenerator<string, Embedding<float>>
{
    private static AiUnavailableException NotConfigured() =>
        new("Yapay zekâ servisi yapılandırılmamış: .env dosyasına AI_API_KEY ekleyip sistemi yeniden başlat.");

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    private static readonly ChatClientMetadata Metadata = new(AiModel.UnconfiguredProvider);

    // Lets features with a rule-based fallback see that no model is configured before calling it.
    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType == typeof(ChatClientMetadata) ? Metadata : null;

    public void Dispose() { }
}
