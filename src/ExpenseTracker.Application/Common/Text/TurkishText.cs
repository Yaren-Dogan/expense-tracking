using System.Globalization;
using ExpenseTracker.Domain.ValueObjects;

namespace ExpenseTracker.Application.Common.Text;

/// <summary>
/// Dates, months and money written the Turkish way for texts users read (notifications, reviews, plans). Formatted by
/// hand: container images may run without ICU culture data, and the texts must read the same everywhere.
/// </summary>
public static class TurkishText
{
    private static readonly string[] Months =
        ["Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"];

    public static string MonthName(int month) => Months[month - 1];

    /// <summary>"Eylül 2026".</summary>
    public static string Month(BudgetMonth month) => $"{Months[month.Month - 1]} {month.Year}";

    /// <summary>"6 Eylül".</summary>
    public static string Date(DateOnly date) => $"{date.Day} {Months[date.Month - 1]}";

    /// <summary>1234.5 -> "₺1.234,50".</summary>
    public static string Money(decimal amount)
    {
        var invariant = amount.ToString("N2", CultureInfo.InvariantCulture);
        return "₺" + invariant.Replace(",", "_").Replace(".", ",").Replace("_", ".");
    }

    /// <summary>Whole lira when the amount has no kuruş: "₺15.000", otherwise "₺1.234,50".</summary>
    public static string MoneyShort(decimal amount) =>
        amount == decimal.Truncate(amount) ? Money(amount)[..^3] : Money(amount);

    /// <summary>True when a model-written text contains a number. Numbers users read must come from code.</summary>
    public static bool HasDigits(string? text) => text is not null && text.Any(char.IsDigit);
}
