using Microsoft.Extensions.AI;

namespace ExpenseTracker.Application.Common.Ai;

/// <summary>What the configured chat client says about itself.</summary>
public static class AiModel
{
    /// <summary>The provider name the stand-in client reports when no API key is configured.</summary>
    public const string UnconfiguredProvider = "unconfigured";

    /// <summary>
    /// False when no provider is configured. Features with a rule-based fallback then skip the model altogether instead of
    /// failing (and logging an error) on every call, which matters for background jobs that run for every ledger.
    /// </summary>
    public static bool IsConfigured(IChatClient client) =>
        client.GetService<ChatClientMetadata>()?.ProviderName != UnconfiguredProvider;

    /// <summary>The model's name for "written by" labels.</summary>
    public static string Name(IChatClient client) => client.GetService<ChatClientMetadata>()?.DefaultModelId ?? "yapay zekâ";
}
