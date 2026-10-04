using System.Diagnostics;
using ExpenseTracker.Application.Common.Exceptions;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Common.Logging;
using MediatR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExpenseTracker.Application.Features.Assistant.Commands.AskAssistant;

/// <summary>Runs one assistant turn in the configured <see cref="AgentMode"/>.</summary>
public sealed class AskAssistantCommandHandler(
    IChatClient chatClient,
    IAppDbContext db,
    IUserContext user,
    ISender mediator,
    ISemanticExpenseSearch search,
    IAppClock clock,
    IUserDirectory users,
    IOptions<AssistantOptions> options,
    ILogger<AskAssistantCommandHandler> logger)
    : IRequestHandler<AskAssistantCommand, AssistantReply>
{
    public async Task<AssistantReply> Handle(AskAssistantCommand request, CancellationToken cancellationToken)
    {
        var today = request.Today ?? clock.Today;
        var tools = new AssistantTools(db, user, mediator, search, users, today);
        var conversation = request.Messages
            .Select(m => new ChatMessage(m.Role == ChatRoles.User ? ChatRole.User : ChatRole.Assistant, m.Content))
            .ToList();

        var stopwatch = Stopwatch.StartNew();
        var reply = options.Value.AgentMode == AgentMode.Native
            ? await RunNativeAsync(today, conversation, tools, cancellationToken)
            : await new PlannedAgent(chatClient, tools, options.Value.MaxToolIterations).RunAsync(today, conversation, cancellationToken);

        if (string.IsNullOrWhiteSpace(reply.Reply))
            throw new AiResponseException("Asistan bir cevap üretemedi. Soruyu biraz farklı sormayı dener misin?");

        logger.AssistantAnswered(user.UserId, stopwatch.ElapsedMilliseconds, string.Join(", ", reply.ToolsUsed));
        return reply;
    }

    /// <summary>
    /// Native mode: the chat client is wrapped in a function-invoking client (see Infrastructure DI). When the model
    /// asks for a tool, the tool runs and its result goes back to the model, until it answers in text.
    /// </summary>
    private async Task<AssistantReply> RunNativeAsync(
        DateOnly today, List<ChatMessage> conversation, AssistantTools tools, CancellationToken ct)
    {
        List<ChatMessage> messages = [new(ChatRole.System, AssistantPrompt.System(today)), .. conversation];
        var chatOptions = new ChatOptions { Tools = tools.All(), ToolMode = ChatToolMode.Auto, Temperature = 0.2f };

        var response = await chatClient.GetResponseAsync(messages, chatOptions, ct);

        var toolsUsed = response.Messages
            .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
            .Select(c => c.Name)
            .Distinct()
            .ToList();

        return new AssistantReply(response.Text.Trim(), toolsUsed);
    }
}
