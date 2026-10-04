using ExpenseTracker.Domain.Common;
using ExpenseTracker.Domain.Entities;
using ExpenseTracker.Domain.Enums;
using ExpenseTracker.Domain.Notifications;
using ExpenseTracker.Domain.Tasks;
using FluentAssertions;

namespace ExpenseTracker.Domain.Tests;

public class HouseholdTaskTests
{
    private static readonly Guid Ledger = Guid.NewGuid(), Yaren = Guid.NewGuid(), Ahmet = Guid.NewGuid(), Can = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 9, 0, 0, TimeSpan.Zero);

    private static HouseholdTask Bill(DateOnly due, TaskRecurrence recurrence = TaskRecurrence.None) =>
        HouseholdTask.Create(Ledger, "  Elektrik faturası ", ExpenseCategory.Bills, Ahmet, Yaren, due, recurrence);

    [Fact]
    public void Title_is_trimmed_and_required()
    {
        Bill(new DateOnly(2026, 10, 15)).Title.Should().Be("Elektrik faturası");

        var act = () => HouseholdTask.Create(Ledger, " ", ExpenseCategory.Bills, Ahmet, Yaren, new DateOnly(2026, 10, 15), TaskRecurrence.None);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Assignee_and_creator_can_complete_but_others_cannot()
    {
        var task = Bill(new DateOnly(2026, 10, 15));

        task.CanBeCompletedBy(Ahmet).Should().BeTrue();
        task.CanBeCompletedBy(Yaren).Should().BeTrue();
        task.CanBeCompletedBy(Can).Should().BeFalse();
        var act = () => task.Complete(Can, Now);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void A_task_is_completed_once_and_records_the_payment()
    {
        var task = Bill(new DateOnly(2026, 10, 15));
        var payment = Expense.Create(Ahmet, Ledger, 450m, ExpenseCategory.Bills, task.Title, new DateOnly(2026, 10, 4), null);

        task.Complete(Ahmet, Now, payment).Should().BeNull("a one-off task has no next occurrence");

        task.IsDone.Should().BeTrue();
        task.CompletedById.Should().Be(Ahmet);
        task.ExpenseId.Should().Be(payment.Id);
        task.PaidAmount.Should().Be(450m);
        var again = () => task.Complete(Ahmet, Now);
        again.Should().Throw<DomainException>();
    }

    [Fact]
    public void A_payment_from_another_ledger_is_rejected()
    {
        var task = Bill(new DateOnly(2026, 10, 15));
        var elsewhere = Expense.Create(Ahmet, Guid.NewGuid(), 450m, ExpenseCategory.Bills, null, new DateOnly(2026, 10, 4), null);

        var act = () => task.Complete(Ahmet, Now, elsewhere);
        act.Should().Throw<DomainException>();
        task.IsDone.Should().BeFalse();
    }

    [Fact]
    public void Completing_a_monthly_task_opens_next_month_in_the_same_series()
    {
        var task = Bill(new DateOnly(2026, 10, 15), TaskRecurrence.Monthly);

        var next = task.Complete(Ahmet, Now)!;

        next.DueOn.Should().Be(new DateOnly(2026, 11, 15));
        next.SeriesId.Should().Be(task.SeriesId);
        next.Id.Should().NotBe(task.Id);
        next.IsDone.Should().BeFalse();
        (next.AssigneeId, next.CreatedById, next.Title, next.Recurrence).Should().Be((Ahmet, Yaren, task.Title, TaskRecurrence.Monthly));
    }

    [Fact]
    public void A_month_end_due_date_is_clamped_in_short_months_and_restored_after()
    {
        var january = Bill(new DateOnly(2027, 1, 31), TaskRecurrence.Monthly);

        var february = january.Complete(Ahmet, Now)!;
        var march = february.Complete(Ahmet, Now)!;

        february.DueOn.Should().Be(new DateOnly(2027, 2, 28));
        march.DueOn.Should().Be(new DateOnly(2027, 3, 31), "the series keeps its original day of month");
    }

    [Fact]
    public void Only_open_tasks_can_be_reassigned()
    {
        var task = Bill(new DateOnly(2026, 10, 15));
        task.AssignTo(Yaren);
        task.AssigneeId.Should().Be(Yaren);

        task.Complete(Yaren, Now);
        var act = () => task.AssignTo(Ahmet);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Long_notification_text_is_shortened_instead_of_failing()
    {
        var n = Notification.Create(Yaren, NotificationKind.TaskAssigned, Guid.NewGuid(), new string('a', 500));

        n.Message.Should().HaveLength(Notification.MessageMaxLength).And.EndWith("…");
        n.MarkRead(Now);
        n.MarkRead(Now.AddHours(1));
        n.ReadAt.Should().Be(Now, "the first read time is kept");
    }
}
