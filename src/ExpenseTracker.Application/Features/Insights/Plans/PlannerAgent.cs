using System.ComponentModel;
using ExpenseTracker.Application.Common.Ai;
using ExpenseTracker.Application.Common.Text;
using ExpenseTracker.Application.Features.Insights.Reviews;
using ExpenseTracker.Domain.Enums;
using ExpenseTracker.Domain.Insights;
using Microsoft.Extensions.AI;

namespace ExpenseTracker.Application.Features.Insights.Plans;

/// <summary>What the planner decides: the categories to trim and a habit to build. No amounts.</summary>
public sealed record PlanDraft(IReadOnlyList<CutChoice> Choices, string Advice, string GeneratedBy);

/// <summary>
/// The budget planner agent. It sees the goal, the monthly averages and the saving needed, may inspect what a category is
/// made of (at most twice) and finishes by choosing 1-3 categories with a level and a reason. <see cref="PlanMath"/>
/// turns that into amounts. Unusable output (essential categories, numbers in its own words) is replaced by the rules.
/// </summary>
internal sealed class PlannerAgent(IChatClient chatClient, string modelName)
{
    private const int MaxInspections = 2;
    private const int MaxCuts = 3;
    private const int MaxReasonLength = 200;

    internal enum PlannerAction
    {
        InspectCategory,
        Finish
    }

    internal sealed record CutStep(
        [property: Description("Kısılacak kategori")] ExpenseCategory Category,
        [property: Description("Light, Medium ya da Strong")] CutLevel Level,
        [property: Description("Neden bu kategori (rakam yok)")] string Reason);

    internal sealed record PlannerStep(
        [property: Description("Bir sonraki adımın kısa gerekçesi")] string Reason,
        [property: Description("InspectCategory ya da Finish")] PlannerAction Action,
        [property: Description("InspectCategory için kategori, yoksa boş")] string Category,
        [property: Description("Finish için kısılacak kategoriler")] List<CutStep> Cuts,
        [property: Description("Finish için alışkanlık önerisi (rakam yok), yoksa boş")] string Advice);

    public async Task<PlanDraft> RunAsync(
        decimal goal, int months, string? purpose, IReadOnlyDictionary<ExpenseCategory, decimal> baseline,
        IReadOnlyList<SpendingRow> recent, CancellationToken ct)
    {
        List<ChatMessage> messages =
        [
            new(ChatRole.System, PlannerPrompt.System(goal, months, baseline, Math.Ceiling(goal / months))),
            new(ChatRole.User, PlannerPrompt.User(purpose))
        ];

        PlannerStep? finish = null;
        var inspected = new HashSet<ExpenseCategory>();
        for (var step = 0; step < MaxInspections + 2 && finish is null; step++)
        {
            var response = await chatClient.GetResponseAsync<PlannerStep>(
                messages, AiJson.Options, new ChatOptions { Temperature = 0 }, useJsonSchemaResponseFormat: true, ct);
            if (!response.TryGetResult(out var plan) || plan is null)
                break;
            if (plan.Action == PlannerAction.Finish)
            {
                finish = plan;
                break;
            }

            if (!Enum.TryParse<ExpenseCategory>(plan.Category, ignoreCase: true, out var category)
                || inspected.Count >= MaxInspections || !inspected.Add(category))
            {
                messages.Add(new ChatMessage(ChatRole.User, "Bu kategori geçersiz ya da zaten incelendi. Şimdi Finish seç."));
                continue;
            }

            messages.Add(new ChatMessage(ChatRole.User,
                $"InspectCategory({category}) sonucu: {Inspect(category, recent)}\nYeterliyse Finish seç."));
        }

        var rules = Rules(baseline, purpose);
        if (finish is null)
            return rules;

        var choices = (finish.Cuts ?? [])
            .Where(c => PlanMath.Flexible.Contains(c.Category) && Enum.IsDefined(c.Level) && baseline.GetValueOrDefault(c.Category) > 0)
            .DistinctBy(c => c.Category)
            .Take(MaxCuts)
            .Select(c => new CutChoice(c.Category, c.Level, Usable(c.Reason) ? ModelText.Clip(c.Reason, MaxReasonLength) : RuleReason(c.Category)))
            .ToList();

        return new PlanDraft(
            choices.Count > 0 ? choices : rules.Choices,
            Usable(finish.Advice) ? finish.Advice.Trim() : rules.Advice,
            choices.Count > 0 ? modelName : MonthReview.RulesAuthor);
    }

    /// <summary>The plan without a model: trim the biggest flexible categories, the two largest harder.</summary>
    public static PlanDraft Rules(IReadOnlyDictionary<ExpenseCategory, decimal> baseline, string? purpose)
    {
        var choices = baseline
            .Where(kv => PlanMath.Flexible.Contains(kv.Key) && kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .Take(MaxCuts)
            .Select((kv, i) => new CutChoice(kv.Key, i < 2 ? CutLevel.Medium : CutLevel.Light, RuleReason(kv.Key)))
            .ToList();
        var advice = string.IsNullOrWhiteSpace(purpose)
            ? "Her ay başında biriktireceğin tutarı ayrı bir hesaba aktar; kalanını bütçeyle harca."
            : $"Her ay başında {purpose.Trim()} için ayıracağın tutarı ayrı bir hesaba aktar; kalanını bütçeyle harca.";
        return new PlanDraft(choices, advice, MonthReview.RulesAuthor);
    }

    private static string RuleReason(ExpenseCategory category) => category switch
    {
        ExpenseCategory.Food => "Dışarıda yemek ve kahve, kısmak için en esnek kalemlerden.",
        ExpenseCategory.Entertainment => "Eğlence harcamaları ihtiyaçtan çok tercihe bağlı.",
        ExpenseCategory.Clothing => "Giyim alışverişleri ertelenebilir.",
        ExpenseCategory.Market => "Liste yaparak ve fiyat karşılaştırarak market harcaması azalır.",
        ExpenseCategory.Transport => "Bazı taksi yolculukları toplu taşımayla değiştirilebilir.",
        _ => "Son aylarda büyük bir esnek kalem."
    };

    /// <summary>What a category was spent on in recent months, grouped by note: the inspect tool, computed in code.</summary>
    private static string Inspect(ExpenseCategory category, IReadOnlyList<SpendingRow> recent)
    {
        var name = CategoryNames.Turkish(category);
        var groups = recent.Where(e => e.Category == category)
            .GroupBy(e => string.IsNullOrWhiteSpace(e.Note) ? name : e.Note.Trim(), StringComparer.CurrentCultureIgnoreCase)
            .Select(g => (Label: g.Key, Count: g.Count(), Total: g.Sum(e => e.Amount)))
            .OrderByDescending(g => g.Total).Take(5).ToList();
        return groups.Count == 0
            ? $"{name}: son aylarda harcama yok."
            : $"{name} harcamalarının en büyükleri: " +
              string.Join(", ", groups.Select(g => $"{g.Label} ({g.Count} kez, toplam {TurkishText.MoneyShort(g.Total)})")) + ".";
    }

    private static bool Usable(string? text) => ModelText.IsUsable(text);
}
