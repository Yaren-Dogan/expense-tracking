using System.Globalization;
using ExpenseTracker.Domain.Tasks;

namespace ExpenseTracker.Application.Features.Tasks;

/// <summary>
/// Notification texts. Dates and money are formatted by hand: container images may run without ICU culture data,
/// and the messages must read the same everywhere.
/// </summary>
public static class TaskMessages
{
    private static readonly string[] Months =
        ["Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"];

    public static string Date(DateOnly date) => $"{date.Day} {Months[date.Month - 1]}";

    public static string Money(decimal amount)
    {
        // 1234.5 -> "1,234.50" -> "1.234,50"
        var invariant = amount.ToString("N2", CultureInfo.InvariantCulture);
        return "₺" + invariant.Replace(",", "_").Replace(".", ",").Replace("_", ".");
    }

    public static string Assigned(string byName, HouseholdTask task) =>
        $"{byName} sana bir görev verdi: {task.Title} · son gün {Date(task.DueOn)}";

    public static string HandedOver(string leaverName, HouseholdTask task) =>
        $"{leaverName} haneden ayrıldı, görevi sana geçti: {task.Title} · son gün {Date(task.DueOn)}";

    public static string DueSoon(HouseholdTask task, DateOnly today)
    {
        var days = task.DueOn.DayNumber - today.DayNumber;
        var when = days == 0 ? "bugün" : days == 1 ? "yarın" : $"{days} gün sonra";
        return $"{task.Title} için son gün {when} ({Date(task.DueOn)})";
    }

    public static string Overdue(HouseholdTask task) => $"{task.Title} gecikti: son gün {Date(task.DueOn)} idi";

    public static string Completed(string byName, HouseholdTask task) =>
        task.PaidAmount is { } amount
            ? $"{byName} tamamladı: {task.Title} · {Money(amount)} ödendi"
            : $"{byName} tamamladı: {task.Title}";
}
