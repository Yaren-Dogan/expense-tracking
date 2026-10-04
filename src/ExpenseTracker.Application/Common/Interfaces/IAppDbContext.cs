using ExpenseTracker.Domain.Entities;
using ExpenseTracker.Domain.Households;
using ExpenseTracker.Domain.Insights;
using ExpenseTracker.Domain.Notifications;
using ExpenseTracker.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ExpenseTracker.Application.Common.Interfaces;

/// <summary>
/// Handlers query with LINQ directly: a repository layer would only re-wrap DbSet.
/// Expenses, budgets, tasks, reviews and plans are already filtered to the current ledger, notifications to the current user, by global
/// query filters; households are not.
/// </summary>
public interface IAppDbContext
{
    DbSet<Expense> Expenses { get; }
    DbSet<Budget> Budgets { get; }
    DbSet<Household> Households { get; }
    DbSet<HouseholdTask> Tasks { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<MonthReview> MonthReviews { get; }
    DbSet<BudgetPlan> BudgetPlans { get; }

    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;

    /// <exception cref="Exceptions.UniqueConstraintViolationException">A unique index rejected the write.</exception>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
