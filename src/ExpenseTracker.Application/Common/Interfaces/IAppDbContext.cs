using ExpenseTracker.Domain.Entities;
using ExpenseTracker.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ExpenseTracker.Application.Common.Interfaces;

/// <summary>
/// Handlers query with LINQ directly: a repository layer would only re-wrap DbSet.
/// Expenses and budgets are already filtered to the current ledger by a global query filter; households are not.
/// </summary>
public interface IAppDbContext
{
    DbSet<Expense> Expenses { get; }
    DbSet<Budget> Budgets { get; }
    DbSet<Household> Households { get; }

    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;

    /// <exception cref="Exceptions.UniqueConstraintViolationException">A unique index rejected the write.</exception>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
