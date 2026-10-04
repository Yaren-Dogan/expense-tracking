using System.ComponentModel;
using ExpenseTracker.Application.Common.Ai;
using ExpenseTracker.Application.Common.Text;
using ExpenseTracker.Domain.Enums;
using ExpenseTracker.Domain.Insights;
using ExpenseTracker.Domain.ValueObjects;
using ExpenseTracker.Domain.Weather;
using Microsoft.Extensions.AI;

namespace ExpenseTracker.Application.Features.Insights.Reviews;

/// <summary>What the review agent decides: which facts matter, and the words around them.</summary>
public sealed record ReviewDraft(string Headline, IReadOnlyList<string> Highlights, string Tip, string GeneratedBy);

/// <summary>
/// The month review agent: a plan-act-observe loop like the assistant's planned mode. It reads the code-computed facts,
/// may drill into a category's expenses (at most twice) and finishes by choosing the highlights and writing a headline
/// and a tip. Everything it returns is checked; anything unusable (unknown fact ids, numbers in its own words) is
/// replaced by the rule-based version, so a review is always produced, with or without a model.
/// </summary>
internal sealed class ReviewAgent(IChatClient chatClient, string modelName)
{
    private const int MaxDrillDowns = 2;

    internal enum ReviewAction
    {
        DrillDown,
        Finish
    }

    /// <summary>One schema for every step; fields the chosen action does not use come back empty.</summary>
    internal sealed record ReviewStep(
        [property: Description("Bir sonraki adımın kısa gerekçesi")] string Reason,
        [property: Description("DrillDown ya da Finish")] ReviewAction Action,
        [property: Description("DrillDown için kategori, yoksa boş")] string Category,
        [property: Description("Finish için başlık (rakam yok), yoksa boş")] string Headline,
        [property: Description("Finish için seçilen gerçeklerin id'leri")] List<string> Highlights,
        [property: Description("Finish için tavsiye (rakam yok), yoksa boş")] string Tip);

    public async Task<ReviewDraft> RunAsync(
        BudgetMonth month, decimal? budget, IReadOnlyList<SpendingRow> expenses,
        IReadOnlyDictionary<ExpenseCategory, IReadOnlyList<SpendingRow>> previous, List<ReviewFact> facts, CancellationToken ct)
    {
        List<ChatMessage> messages =
        [
            new(ChatRole.System, ReviewPrompt.System(month, facts)),
            new(ChatRole.User, "Değerlendirmeyi hazırla.")
        ];

        ReviewStep? finish = null;
        var drilled = new HashSet<ExpenseCategory>();
        for (var step = 0; step < MaxDrillDowns + 2 && finish is null; step++)
        {
            var response = await chatClient.GetResponseAsync<ReviewStep>(
                messages, AiJson.Options, new ChatOptions { Temperature = 0 }, useJsonSchemaResponseFormat: true, ct);
            if (!response.TryGetResult(out var plan) || plan is null)
                break;

            if (plan.Action == ReviewAction.Finish)
            {
                finish = plan;
                break;
            }

            if (!Enum.TryParse<ExpenseCategory>(plan.Category, ignoreCase: true, out var category) || !drilled.Add(category)
                || drilled.Count > MaxDrillDowns)
            {
                messages.Add(new ChatMessage(ChatRole.User, "Bu kategori geçersiz ya da zaten incelendi. Şimdi Finish seç."));
                continue;
            }

            var fact = DrillDown(category, expenses, previous.GetValueOrDefault(category) ?? []);
            facts.Add(fact);
            messages.Add(new ChatMessage(ChatRole.User,
                $"DrillDown({category}) sonucu, id {fact.Id}: {fact.Sentence}\nYeterliyse Finish seç."));
        }

        var rules = Rules(month, budget, expenses.Sum(e => e.Amount), facts);
        if (finish is null)
            return rules;

        var known = facts.ToDictionary(f => f.Id, f => f.Sentence);
        var chosen = (finish.Highlights ?? []).Distinct().Where(known.ContainsKey).Take(MonthReview.MaxHighlights)
            .Select(id => known[id]).ToList();

        // The model's own words may not carry numbers (they could only be invented) or links (a crafted expense note could
        // try to plant one in a text every member reads). Fall back part by part.
        var headline = Usable(finish.Headline) ? finish.Headline.Trim() : rules.Headline;
        var tip = Usable(finish.Tip) ? finish.Tip.Trim() : rules.Tip;
        return new ReviewDraft(headline, chosen.Count > 0 ? chosen : rules.Highlights, tip, modelName);
    }

    /// <summary>The review without a model: the best-scored facts, a headline from the month's sky, a tip from the biggest rise.</summary>
    public static ReviewDraft Rules(BudgetMonth month, decimal? budget, decimal total, IReadOnlyList<ReviewFact> facts)
    {
        var headline = MonthFacts.Sky(month, budget, total) switch
        {
            WeatherCondition.Sunny => "Güneşli bir ay: bütçenin rahatça içinde kaldın.",
            WeatherCondition.PartlyCloudy => "Parçalı bulutlu bir ay: bütçenin içinde kaldın.",
            WeatherCondition.Cloudy => "Bulutlu bir ay: bütçeyi tam sınırında tutturdun.",
            WeatherCondition.Rainy => "Yağmurlu bir ay: bütçe biraz aşıldı.",
            WeatherCondition.Stormy => "Fırtınalı bir ay: bütçe epey aşıldı.",
            _ => "Bütçesiz bir ay: harcamalarının özeti burada."
        };

        // Only a flexible category deserves a "set a limit" tip; a rise in bills or health is not a habit to change.
        var risen = facts
            .Where(f => f.Id.StartsWith("cat-up:", StringComparison.Ordinal) || f.Id.StartsWith("cat-new:", StringComparison.Ordinal))
            .Select(f => Enum.TryParse<ExpenseCategory>(f.Id[(f.Id.IndexOf(':') + 1)..], out var c) ? c : (ExpenseCategory?)null)
            .FirstOrDefault(c => c is { } category && Plans.PlanMath.Flexible.Contains(category));
        var tip = risen is { } flexible
            ? $"{CategoryNames.Turkish(flexible)} harcamasına önümüzdeki ay haftalık bir sınır koymayı dene."
            : budget is { } b && total > b
                ? "Önümüzdeki ay her sabah günlük payına bakmak aşımı önlemenin en kolay yolu."
                : "Bu düzeni koru; ay sonunda artan parayı birikime ayırabilirsin.";

        var highlights = facts.OrderByDescending(f => f.Score).Take(3).Select(f => f.Sentence).ToList();
        return new ReviewDraft(headline, highlights, tip, MonthReview.RulesAuthor);
    }

    /// <summary>What a category was made of this month and the month before: the drill-down tool, computed in code.</summary>
    private static ReviewFact DrillDown(ExpenseCategory category, IReadOnlyList<SpendingRow> expenses, IReadOnlyList<SpendingRow> previous)
    {
        var now = expenses.Where(e => e.Category == category).ToList();
        var name = CategoryNames.Turkish(category);
        var biggest = now
            .GroupBy(e => string.IsNullOrWhiteSpace(e.Note) ? name : e.Note.Trim(), StringComparer.CurrentCultureIgnoreCase)
            .Select(g => (Label: g.Key, Count: g.Count(), Total: g.Sum(e => e.Amount)))
            .OrderByDescending(g => g.Total).Take(3)
            .Select(g => g.Count > 1 ? $"{g.Label} ({g.Count} kez, {TurkishText.MoneyShort(g.Total)})" : $"{g.Label} ({TurkishText.MoneyShort(g.Total)})");

        var sentence = now.Count == 0
            ? $"{name}: bu ay harcama yok; önceki ay {previous.Count} harcama, {TurkishText.MoneyShort(previous.Sum(e => e.Amount))}."
            : $"{name}: bu ay {now.Count} harcama, {TurkishText.MoneyShort(now.Sum(e => e.Amount))}; en büyükleri {string.Join(", ", biggest)}. " +
              $"Önceki ay {previous.Count} harcama, {TurkishText.MoneyShort(previous.Sum(e => e.Amount))}.";
        return new ReviewFact($"drill:{category}", sentence, 30);
    }

    private static bool Usable(string? text) => ModelText.IsUsable(text);
}
