using MediatR;

namespace ExpenseTracker.Application.Features.Assistant.Commands.AskAssistant;

/// <summary>One turn of the budget assistant. The client keeps the history and sends it each time (stateless server).</summary>
public sealed record AskAssistantCommand(IReadOnlyList<ChatTurn> Messages, DateOnly? Today) : IRequest<AssistantReply>;

/// <param name="Role"><c>user</c> or <c>assistant</c>.</param>
public sealed record ChatTurn(string Role, string Content);

/// <param name="ToolsUsed">Names of the tools the model called, shown to the user for transparency.</param>
public sealed record AssistantReply(string Reply, IReadOnlyList<string> ToolsUsed);
