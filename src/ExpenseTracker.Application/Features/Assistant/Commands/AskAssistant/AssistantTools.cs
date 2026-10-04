using System.ComponentModel;
using ExpenseTracker.Application.Common.Ai;
using ExpenseTracker.Application.Common.Exceptions;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Features.Weather.Queries.GetWeather;
using ExpenseTracker.Domain.Enums;
using ExpenseTracker.Domain.Weather;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace ExpenseTracker.Application.Features.Assistant.Commands.AskAssistant;

/// <summary>
/// The functions the model may call. Every tool reads the ledger from <see cref="IUserContext"/>, never from a
/// model-supplied argument, so the model cannot reach another user's or household's data however it is prompted.
/// Tools are read-only and return compact, Turkish-labelled objects to keep the context small.
/// Invalid arguments come back as an error text the model can read and correct, not as an exception.
/// </summary>
public sealed class AssistantTools(
    IAppDbContext db,
    IUserContext user,
    ISender mediator,
    ISemanticExpenseSearch search,
    IUserDirectory users,
    DateOnly today)
{
    private const int MaxRangeDays = 366;
    private const int MaxListed = 20;
    private const int MaxSearchResults = 15;

    public IList<AITool> All() =>
    [
        AIFunctionFactory.Create(GetBudgetStatusAsync, "get_budget_status",
            "Bugünün bütçe havası, günlük pay, bugün harcanan, bu ay harcanan, kalan bütçe ve ay sonu tahmini."),
        AIFunctionFactory.Create(GetSpendingByCategoryAsync, "get_spending_by_category",
            "Verilen tarih aralığında kategori bazında toplam harcama ve işlem sayısı."),
        AIFunctionFactory.Create(ListExpensesAsync, "list_expenses",
            "Verilen tarih aralığındaki harcamaları listeler (en yeniden eskiye, en fazla 20). Kategori opsiyonel."),
        AIFunctionFactory.Create(SearchExpensesAsync, "search_expenses",
            "Anlamına göre harcama arar. Örn. 'kahve' sorgusu 'Starbucks latte' kaydını da bulur. Bulunanların toplamını da döner.")
    ];

    public async Task<object> GetBudgetStatusAsync(CancellationToken cancellationToken = default)
    {
        var w = await mediator.Send(new GetWeatherQuery(today), cancellationToken);
        if (w.Budget is null)
            return new { bütçe = "Bu ay için bütçe belirlenmemiş", buAyHarcanan = w.SpentThisMonth };

        var özet = w.Today is null
            ? $"Bu ay {Money(w.SpentThisMonth)} harcandı, bütçe {Money(w.Budget.Value)}, kalan {Money(w.RemainingBudget ?? 0)}."
            : $"Bu ay {Money(w.Budget.Value)} bütçenin {Money(w.SpentThisMonth)} kadarı harcandı, {Money(w.RemainingBudget ?? 0)} kaldı. " +
              $"Bugünün havası {WeatherName(w.Today.Condition)}: bugün {Money(w.Today.SpentToday)} harcandı, günlük pay {Money(w.Today.DailyAllowance)}, " +
              $"gönül rahatlığıyla {Money(w.Today.SafeToSpend)} daha harcanabilir." +
              (w.Forecast is { } f
                  ? f.Difference >= 0
                      ? $" Bu hızla ay sonunda {Money(f.Difference)} artar."
                      : $" Bu hızla ay sonunda bütçe {Money(-f.Difference)} aşılır."
                  : "");

        return new
        {
            özet,
            ay = w.Month,
            aylıkBütçe = w.Budget,
            buAyHarcanan = w.SpentThisMonth,
            kalanBütçe = w.RemainingBudget,
            bugününHavası = w.Today is null ? null : WeatherName(w.Today.Condition),
            günlükPay = w.Today?.DailyAllowance,
            bugünHarcanan = w.Today?.SpentToday,
            gönülRahatlığıylaHarcanabilir = w.Today?.SafeToSpend,
            aySonuTahmini = w.Forecast?.ProjectedSpend,
            aySonuFarkı = w.Forecast?.Difference
        };
    }

    public async Task<object> GetSpendingByCategoryAsync(
        [Description("Başlangıç tarihi, yyyy-MM-dd")] string from,
        [Description("Bitiş tarihi (dahil), yyyy-MM-dd")] string to,
        CancellationToken cancellationToken = default)
    {
        if (ParseRange(from, to) is not (var start, var end))
            return RangeError;

        var ledgerId = user.LedgerId;
        var rows = await db.Expenses.AsNoTracking()
            .Where(e => e.LedgerId == ledgerId && e.SpentOn >= start && e.SpentOn <= end)
            .GroupBy(e => e.Category)
            .Select(g => new { Category = g.Key, Total = g.Sum(e => e.Amount), Count = g.Count() })
            .ToListAsync(cancellationToken);

        var ordered = rows.OrderByDescending(r => r.Total).ToList();
        var total = ordered.Sum(r => r.Total);
        var özet = ordered.Count == 0
            ? $"{start:yyyy-MM-dd} – {end:yyyy-MM-dd} arasında harcama yok."
            : $"{start:yyyy-MM-dd} – {end:yyyy-MM-dd} arasında toplam {Money(total)} harcandı. En çok harcanan kategori " +
              $"{CategoryNames.Turkish(ordered[0].Category)}: {Money(ordered[0].Total)} (payı %{Math.Round(ordered[0].Total / total * 100)})." +
              (ordered.Count > 1 ? $" İkinci sırada {CategoryNames.Turkish(ordered[1].Category)}: {Money(ordered[1].Total)}." : "");

        return new
        {
            özet,
            aralık = $"{start:yyyy-MM-dd} – {end:yyyy-MM-dd}",
            toplam = total,
            kategoriler = ordered
                .Select(r => new { kategori = CategoryNames.Turkish(r.Category), toplam = r.Total, adet = r.Count })
        };
    }

    public async Task<object> ListExpensesAsync(
        [Description("Başlangıç tarihi, yyyy-MM-dd")] string from,
        [Description("Bitiş tarihi (dahil), yyyy-MM-dd")] string to,
        [Description("Opsiyonel kategori: Market, Food, Transport, Bills, Entertainment, Health, Clothing, Other")]
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        if (ParseRange(from, to) is not (var start, var end))
            return RangeError;

        ExpenseCategory? filter = null;
        if (!string.IsNullOrWhiteSpace(category))
        {
            if (!Enum.TryParse<ExpenseCategory>(category, ignoreCase: true, out var parsed))
                return new { hata = $"Bilinmeyen kategori '{category}'. Geçerli değerler: {string.Join(", ", Enum.GetNames<ExpenseCategory>())}" };
            filter = parsed;
        }

        var ledgerId = user.LedgerId;
        var items = await db.Expenses.AsNoTracking()
            .Where(e => e.LedgerId == ledgerId && e.SpentOn >= start && e.SpentOn <= end)
            .Where(e => filter == null || e.Category == filter)
            .OrderByDescending(e => e.SpentOn).ThenByDescending(e => e.CreatedAt)
            .Take(MaxListed)
            .Select(e => new { e.SpentOn, e.Category, e.Note, e.Amount, e.UserId })
            .ToListAsync(cancellationToken);

        // In a household "who" matters ("Ayşe bu hafta ne harcadı?"); for a personal ledger it is always the user.
        var names = await users.GetDisplayNamesAsync(items.Select(e => e.UserId).Distinct(), cancellationToken);
        return items.Select(e => new
        {
            tarih = e.SpentOn.ToString("yyyy-MM-dd"),
            kim = names.GetValueOrDefault(e.UserId),
            kategori = CategoryNames.Turkish(e.Category),
            not = e.Note,
            tutar = e.Amount
        });
    }

    public async Task<object> SearchExpensesAsync(
        [Description("Aranacak şey, örn. 'kahve', 'taksi', 'abonelikler'")] string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new { hata = "Arama metni boş olamaz." };

        IReadOnlyList<ExpenseMatch> matches;
        try
        {
            matches = await search.SearchAsync(user.LedgerId, query, MaxSearchResults, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or AiUnavailableException)
        {
            // Search needs the embedding model; if it is down, the agent can still fall back to list/category tools.
            return new { hata = "Anlamsal arama şu an kullanılamıyor. list_expenses ya da get_spending_by_category ile dene." };
        }

        var total = matches.Sum(m => m.Expense.Amount);
        var özet = matches.Count == 0
            ? $"'{query}' ile ilgili harcama bulunamadı."
            : $"'{query}' ile ilgili {matches.Count} harcama bulundu, toplam {Money(total)}. " +
              $"Tarih aralığı {matches.Min(m => m.Expense.SpentOn):yyyy-MM-dd} – {matches.Max(m => m.Expense.SpentOn):yyyy-MM-dd}.";
        return new
        {
            özet,
            bulunan = matches.Count,
            toplam = total,
            harcamalar = matches.Select(m => new
            {
                tarih = m.Expense.SpentOn.ToString("yyyy-MM-dd"),
                kategori = CategoryNames.Turkish(m.Expense.Category),
                not = m.Expense.Note,
                tutar = m.Expense.Amount
            })
        };
    }

    // Facts the user will read are computed and formatted here, not by the model: small models misread
    // unsorted numbers and mislabel fields, but reliably repeat a ready-made sentence.
    private static string Money(decimal value) => value.ToString("C2", TurkishCulture.Instance);

    private static string WeatherName(WeatherCondition condition) => condition switch
    {
        WeatherCondition.Sunny => "güneşli",
        WeatherCondition.PartlyCloudy => "parçalı bulutlu",
        WeatherCondition.Cloudy => "bulutlu",
        WeatherCondition.Rainy => "yağmurlu",
        _ => "fırtınalı"
    };

    private static readonly object RangeError = new
    {
        hata = $"Tarihler yyyy-MM-dd formatında olmalı, başlangıç bitişten sonra olmamalı ve aralık en fazla {MaxRangeDays} gün olmalı."
    };

    private static (DateOnly Start, DateOnly End)? ParseRange(string from, string to)
    {
        if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", out var start) ||
            !DateOnly.TryParseExact(to, "yyyy-MM-dd", out var end) ||
            start > end || end.DayNumber - start.DayNumber > MaxRangeDays)
        {
            return null;
        }

        return (start, end);
    }
}
