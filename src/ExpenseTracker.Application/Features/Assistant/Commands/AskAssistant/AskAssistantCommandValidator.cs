using ExpenseTracker.Application.Common.Interfaces;
using FluentValidation;

namespace ExpenseTracker.Application.Features.Assistant.Commands.AskAssistant;

public sealed class AskAssistantCommandValidator : AbstractValidator<AskAssistantCommand>
{
    public const int MaxMessages = 20;
    public const int MaxMessageLength = 1000;

    public AskAssistantCommandValidator(IAppClock clock)
    {
        RuleFor(x => x.Messages).NotEmpty().Must(m => m.Count <= MaxMessages)
            .WithMessage($"At most {MaxMessages} messages can be sent.");
        RuleFor(x => x.Messages).Must(m => m.Count > 0 && m[^1].Role == ChatRoles.User)
            .WithMessage("The last message must come from the user.");
        RuleForEach(x => x.Messages).ChildRules(turn =>
        {
            turn.RuleFor(t => t.Role).Must(r => r is ChatRoles.User or ChatRoles.Assistant)
                .WithMessage("Role must be 'user' or 'assistant'.");
            turn.RuleFor(t => t.Content).NotEmpty().MaximumLength(MaxMessageLength);
        });
        RuleFor(x => x.Today!.Value)
            .Must(d => d <= clock.Today.AddDays(1) && d >= clock.Today.AddDays(-1))
            .WithMessage("Today must be the client's current date.")
            .OverridePropertyName(nameof(AskAssistantCommand.Today))
            .When(x => x.Today is not null);
    }
}

public static class ChatRoles
{
    public const string User = "user";
    public const string Assistant = "assistant";
}
