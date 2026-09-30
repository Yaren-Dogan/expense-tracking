using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ExpenseTracker.Domain.Common;

namespace ExpenseTracker.Domain.ValueObjects;

/// <summary>A calendar month, written as <c>yyyy-MM</c> on the wire.</summary>
public readonly record struct BudgetMonth
{
    public const int MinYear = 2000;
    public const int MaxYear = 2100;

    public int Year { get; }
    public int Month { get; }

    public BudgetMonth(int year, int month)
    {
        if (year is < MinYear or > MaxYear)
            throw new DomainException($"Year must be between {MinYear} and {MaxYear}.");
        if (month is < 1 or > 12)
            throw new DomainException("Month must be between 1 and 12.");

        Year = year;
        Month = month;
    }

    public static BudgetMonth From(DateOnly date) => new(date.Year, date.Month);

    public int DaysInMonth => DateTime.DaysInMonth(Year, Month);
    public DateOnly FirstDay => new(Year, Month, 1);
    public DateOnly LastDay => new(Year, Month, DaysInMonth);

    public static bool TryParse([NotNullWhen(true)] string? value, out BudgetMonth month)
    {
        month = default;
        if (!DateOnly.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || date.Year is < MinYear or > MaxYear)
        {
            return false;
        }

        month = From(date);
        return true;
    }

    public override string ToString() => $"{Year:D4}-{Month:D2}";
}
