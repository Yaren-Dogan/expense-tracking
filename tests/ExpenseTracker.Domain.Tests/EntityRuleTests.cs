using ExpenseTracker.Domain.Common;
using ExpenseTracker.Domain.Entities;
using ExpenseTracker.Domain.Enums;
using ExpenseTracker.Domain.ValueObjects;
using FluentAssertions;

namespace ExpenseTracker.Domain.Tests;

public class EntityRuleTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly DateOnly Day = new(2026, 9, 1);

    [Fact]
    public void Expense_trims_the_note_and_drops_blank_values()
    {
        var expense = Expense.Create(User, 12.5m, ExpenseCategory.Food, "  simit  ", Day, " ");

        expense.Note.Should().Be("simit");
        expense.IdempotencyKey.Should().BeNull();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1000000.01")]
    [InlineData("1.005")]
    public void Expense_rejects_invalid_amounts(string amount)
    {
        var act = () => Expense.Create(User, decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture),
            ExpenseCategory.Food, null, Day, null);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Expense_requires_a_user()
    {
        var act = () => Expense.Create(Guid.Empty, 10m, ExpenseCategory.Food, null, Day, null);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Budget_change_keeps_the_same_rules_as_create()
    {
        var budget = Budget.Create(User, new BudgetMonth(2026, 9), 3000m);

        budget.ChangeAmount(4500m);
        budget.Amount.Should().Be(4500m);

        var act = () => budget.ChangeAmount(0m);
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("2026-09", true)]
    [InlineData("2026-9", false)]
    [InlineData("1999-12", false)]
    [InlineData("2026-13", false)]
    [InlineData("september", false)]
    [InlineData(null, false)]
    public void BudgetMonth_parses_only_yyyy_MM(string? value, bool ok)
    {
        BudgetMonth.TryParse(value, out var month).Should().Be(ok);
        if (ok) month.ToString().Should().Be(value);
    }

    [Fact]
    public void BudgetMonth_knows_its_bounds()
    {
        var february = new BudgetMonth(2028, 2);

        february.DaysInMonth.Should().Be(29);
        february.LastDay.Should().Be(new DateOnly(2028, 2, 29));
    }
}
