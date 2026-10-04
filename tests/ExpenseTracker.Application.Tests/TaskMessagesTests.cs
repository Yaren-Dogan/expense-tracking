using ExpenseTracker.Application.Features.Tasks;
using ExpenseTracker.Domain.Enums;
using ExpenseTracker.Domain.Tasks;
using FluentAssertions;

namespace ExpenseTracker.Application.Tests;

public class TaskMessagesTests
{
    private static HouseholdTask Bill(DateOnly due) =>
        HouseholdTask.Create(Guid.NewGuid(), "İnternet faturası", ExpenseCategory.Bills, Guid.NewGuid(), Guid.NewGuid(), due, TaskRecurrence.None);

    [Theory]
    [InlineData("450", "₺450,00")]
    [InlineData("1234.5", "₺1.234,50")]
    [InlineData("1000000", "₺1.000.000,00")]
    public void Money_is_written_the_Turkish_way(string amount, string expected) =>
        TaskMessages.Money(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)).Should().Be(expected);

    [Theory]
    [InlineData(0, "İnternet faturası için son gün bugün (4 Ekim)")]
    [InlineData(1, "İnternet faturası için son gün yarın (5 Ekim)")]
    [InlineData(3, "İnternet faturası için son gün 3 gün sonra (7 Ekim)")]
    public void Due_soon_says_when(int daysLeft, string expected)
    {
        var today = new DateOnly(2026, 10, 4);
        TaskMessages.DueSoon(Bill(today.AddDays(daysLeft)), today).Should().Be(expected);
    }

    [Fact]
    public void Assigned_names_the_sender_and_the_due_date() =>
        TaskMessages.Assigned("Yaren", Bill(new DateOnly(2026, 12, 1)))
            .Should().Be("Yaren sana bir görev verdi: İnternet faturası · son gün 1 Aralık");
}
