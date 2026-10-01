namespace ExpenseTracker.Application.Features.Assistant.Commands.AskAssistant;

public enum AgentMode
{
    /// <summary>
    /// The app runs the loop: each step the model picks a tool through schema-constrained JSON, the app runs it.
    /// Reliable on small local models, whose free-form tool calling is unreliable.
    /// </summary>
    Planned,

    /// <summary>The provider's native tool calling drives the loop. For capable hosted models (Claude, GPT, 7B+).</summary>
    Native
}

public sealed class AssistantOptions
{
    public const string SectionName = "Ai";

    public AgentMode AgentMode { get; set; } = AgentMode.Planned;

    /// <summary>Upper bound on tool rounds per question, so a confused model cannot loop forever.</summary>
    public int MaxToolIterations { get; set; } = 5;
}
