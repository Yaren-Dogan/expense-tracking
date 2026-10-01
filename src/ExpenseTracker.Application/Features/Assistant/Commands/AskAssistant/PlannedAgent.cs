using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ExpenseTracker.Application.Common.Ai;
using Microsoft.Extensions.AI;

namespace ExpenseTracker.Application.Features.Assistant.Commands.AskAssistant;

/// <summary>
/// A plan-act-observe loop that does not depend on the model's native tool calling.
/// Each step the model must answer with a JSON object matching <see cref="AgentStep"/>; Ollama enforces the schema
/// during decoding, so even a 3B model always produces a valid, executable choice. The app runs the chosen tool,
/// appends the result as an observation and asks again, until the model chooses to answer or the step limit is hit.
/// The reply is built from the tools' code-computed summaries, verbatim. Measured on a 3B model: it picks tools
/// reliably under a schema, but when asked to rewrite results in prose it misreads, invents percentages and garbles
/// sums, and any "advice" it adds restates numbers wrongly. So in this mode the model decides, the code reports.
/// Free-form, commentary-style answers belong to <see cref="AgentMode.Native"/> with a capable model.
/// </summary>
internal sealed class PlannedAgent(IChatClient chatClient, AssistantTools tools, int maxSteps)
{
    internal enum AgentAction
    {
        GetBudgetStatus,
        GetSpendingByCategory,
        ListExpenses,
        SearchExpenses,
        Answer
    }

    /// <summary>Unused fields come back as empty strings: no nullable types, so every provider accepts the schema.</summary>
    internal sealed record AgentStep(
        [property: Description("Bir sonraki adımın kısa gerekçesi")] string Reason,
        [property: Description("Çağrılacak araç ya da Answer")] AgentAction Action,
        [property: Description("Başlangıç tarihi yyyy-MM-dd (GetSpendingByCategory, ListExpenses), yoksa boş")] string From,
        [property: Description("Bitiş tarihi yyyy-MM-dd (GetSpendingByCategory, ListExpenses), yoksa boş")] string To,
        [property: Description("Opsiyonel kategori (ListExpenses), yoksa boş")] string Category,
        [property: Description("Arama metni (SearchExpenses), yoksa boş")] string Query);

    private static readonly Dictionary<AgentAction, string> ToolNames = new()
    {
        [AgentAction.GetBudgetStatus] = "get_budget_status",
        [AgentAction.GetSpendingByCategory] = "get_spending_by_category",
        [AgentAction.ListExpenses] = "list_expenses",
        [AgentAction.SearchExpenses] = "search_expenses"
    };

    public async Task<AssistantReply> RunAsync(
        DateOnly today, IReadOnlyList<ChatMessage> conversation, CancellationToken ct)
    {
        var observations = new List<Observation>();
        var toolsUsed = new List<string>();

        for (var step = 0; step < maxSteps; step++)
        {
            var plan = await PlanAsync(today, conversation, observations, ct);
            if (plan is null || plan.Action == AgentAction.Answer)
                break;

            var call = Describe(plan);
            // A repeated identical call adds nothing; stop and answer with what we have.
            if (observations.Any(o => o.Call == call))
                break;

            var result = await ExecuteAsync(plan, ct);
            observations.Add(Observation.From(call, JsonSerializer.Serialize(result, AiJson.Options)));
            toolsUsed.Add(ToolNames[plan.Action]);
        }

        var answer = await AnswerAsync(today, conversation, observations, ct);
        return new AssistantReply(answer, toolsUsed.Distinct().ToList());
    }

    private async Task<AgentStep?> PlanAsync(
        DateOnly today, IReadOnlyList<ChatMessage> conversation, List<Observation> observations,
        CancellationToken ct)
    {
        List<ChatMessage> messages = [new(ChatRole.System, AssistantPrompt.Planner(today)), .. conversation];
        if (observations.Count > 0)
            messages.Add(new ChatMessage(ChatRole.User, ObservationsText(observations) +
                "\nBu sonuçlar soruyu cevaplamaya yetiyorsa Action=Answer seç; yetmiyorsa başka bir araç seç."));

        var response = await chatClient.GetResponseAsync<AgentStep>(
            messages, AiJson.Options, new ChatOptions { Temperature = 0 }, useJsonSchemaResponseFormat: true, ct);

        return response.TryGetResult(out var plan) ? plan : null;
    }

    private async Task<string> AnswerAsync(
        DateOnly today, IReadOnlyList<ChatMessage> conversation, List<Observation> observations, CancellationToken ct)
    {
        var facts = observations.Select(o => o.Summary).OfType<string>().ToList();
        return facts.Count > 0
            ? string.Join(" ", facts)
            : await FreeTextAnswerAsync(today, conversation, observations, ct);
    }

    /// <summary>No numbers to protect (a greeting, or a tool error): let the model reply in its own words.</summary>
    private async Task<string> FreeTextAnswerAsync(
        DateOnly today, IReadOnlyList<ChatMessage> conversation, List<Observation> observations, CancellationToken ct)
    {
        List<ChatMessage> messages = [new(ChatRole.System, AssistantPrompt.System(today)), .. conversation];
        messages.Add(new ChatMessage(ChatRole.User, observations.Count == 0
            ? "Bu soru için veri gerekmiyor. Kısaca cevap ver."
            : ObservationsText(observations) + "\nAraçlar bir hata döndürdü. Kullanıcıya kısaca ne olduğunu ve soruyu nasıl sorabileceğini söyle."));

        var response = await chatClient.GetResponseAsync(messages, new ChatOptions { Temperature = 0.2f }, ct);
        return response.Text.Trim();
    }

    private Task<object> ExecuteAsync(AgentStep plan, CancellationToken ct) => plan.Action switch
    {
        AgentAction.GetBudgetStatus => tools.GetBudgetStatusAsync(ct),
        AgentAction.GetSpendingByCategory => tools.GetSpendingByCategoryAsync(plan.From ?? "", plan.To ?? "", ct),
        AgentAction.ListExpenses => tools.ListExpensesAsync(plan.From ?? "", plan.To ?? "", NullIfEmpty(plan.Category), ct),
        AgentAction.SearchExpenses => tools.SearchExpensesAsync(plan.Query ?? "", ct),
        _ => throw new ArgumentOutOfRangeException(nameof(plan), plan.Action, "Not a tool.")
    };

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string Describe(AgentStep p) => p.Action switch
    {
        AgentAction.GetSpendingByCategory => $"{ToolNames[p.Action]}(from={p.From}, to={p.To})",
        AgentAction.ListExpenses => $"{ToolNames[p.Action]}(from={p.From}, to={p.To}, category={p.Category})",
        AgentAction.SearchExpenses => $"{ToolNames[p.Action]}(query={p.Query})",
        _ => $"{ToolNames[p.Action]}()"
    };

    private static string ObservationsText(List<Observation> observations)
    {
        var sb = new StringBuilder("Araç sonuçları:\n");
        foreach (var o in observations)
            sb.Append("- ").Append(o.Call).Append(" => ").AppendLine(o.Result);
        return sb.ToString();
    }

    /// <param name="Summary">The tool's code-computed "özet" sentence, when it produced one.</param>
    private sealed record Observation(string Call, string Result, string? Summary)
    {
        public static Observation From(string call, string resultJson)
        {
            using var doc = JsonDocument.Parse(resultJson);
            var summary = doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("özet", out var s) && s.ValueKind == JsonValueKind.String
                    ? s.GetString()
                    : null;
            return new Observation(call, resultJson, summary);
        }
    }
}
