using ExpenseTracker.Application.Common.Text;
using ExpenseTracker.Domain.Tasks;

namespace ExpenseTracker.Application.Features.Tasks;

/// <summary>Notification texts for tasks.</summary>
public static class TaskMessages
{
    public static string Date(DateOnly date) => TurkishText.Date(date);

    public static string Money(decimal amount) => TurkishText.Money(amount);

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
