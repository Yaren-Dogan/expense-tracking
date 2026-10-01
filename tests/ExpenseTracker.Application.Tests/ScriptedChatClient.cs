using Microsoft.Extensions.AI;

namespace ExpenseTracker.Application.Tests;

/// <summary>
/// A deterministic stand-in for a model: replies come from a script and every request is recorded,
/// so tests can assert both what the app does with an answer and what it sent to the model.
/// </summary>
public sealed class ScriptedChatClient(params string[] replies) : IChatClient
{
    private readonly Queue<string> _replies = new(replies);

    public List<List<ChatMessage>> Requests { get; } = [];
    public List<ChatOptions?> Options { get; } = [];

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Requests.Add(messages.ToList());
        Options.Add(options);
        var reply = _replies.Count > 0 ? _replies.Dequeue() : throw new InvalidOperationException("Script exhausted.");
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
