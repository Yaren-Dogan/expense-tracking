using System.ComponentModel;
using System.Diagnostics;
using ExpenseTracker.Application.Common.Ai;
using ExpenseTracker.Application.Common.Exceptions;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Common.Logging;
using ExpenseTracker.Domain.Entities;
using ExpenseTracker.Domain.Enums;
using MediatR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Application.Features.Expenses.Queries.ParseExpense;

public sealed class ParseExpenseQueryHandler(
    IChatClient chatClient,
    IAppClock clock,
    ILogger<ParseExpenseQueryHandler> logger)
    : IRequestHandler<ParseExpenseQuery, ParsedExpenseDto>
{
    private const int MaxDaysAgo = 365;

    /// <summary>What the model is asked to return. The JSON schema sent to the model is generated from this type.</summary>
    internal sealed record ExpenseDraft(
        [property: Description("Tutar, sadece sayı")] decimal Amount,
        [property: Description("Kategori")] ExpenseCategory Category,
        [property: Description("Kaç gün önce: bugün 0, dün 1")] int DaysAgo,
        [property: Description("Harcamanın 2-4 kelimelik kısa adı")] string? Note);

    public async Task<ParsedExpenseDto> Handle(ParseExpenseQuery request, CancellationToken cancellationToken)
    {
        var today = request.Today ?? clock.Today;
        List<ChatMessage> messages =
        [
            new(ChatRole.System, $"{ExpenseParsingPrompt.System}\n{ExpenseParsingPrompt.Today(today)}"),
            new(ChatRole.User, request.Text)
        ];

        var stopwatch = Stopwatch.StartNew();
        var response = await chatClient.GetResponseAsync<ExpenseDraft>(
            messages,
            AiJson.Options,
            new ChatOptions { Temperature = 0 },
            useJsonSchemaResponseFormat: true,
            cancellationToken);

        if (!response.TryGetResult(out var draft) || draft is null || draft.Amount <= 0)
        {
            logger.ExpenseParseFailed(request.Text.Length, stopwatch.ElapsedMilliseconds);
            throw new AiResponseException("Bu cümleden bir harcama çıkaramadım. Tutarı da yazmayı dener misin?");
        }

        // The model only says "how many days ago"; calendar arithmetic stays in code, where it cannot be wrong.
        var spentOn = today.AddDays(-Math.Clamp(draft.DaysAgo, 0, MaxDaysAgo));
        var amount = Math.Min(decimal.Round(draft.Amount, 2, MidpointRounding.AwayFromZero), Expense.MaxAmount);
        var category = Enum.IsDefined(draft.Category) ? draft.Category : ExpenseCategory.Other;
        var note = draft.Note?.Trim() is { Length: > 0 } n ? n[..Math.Min(n.Length, Expense.NoteMaxLength)] : null;

        logger.ExpenseParsed(category, stopwatch.ElapsedMilliseconds);
        return new ParsedExpenseDto(amount, category, note, spentOn);
    }
}
