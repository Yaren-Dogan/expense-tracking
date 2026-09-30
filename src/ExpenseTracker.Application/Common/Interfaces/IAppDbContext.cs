using ExpenseTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ExpenseTracker.Application.Common.Interfaces;

/// <summary>
/// Handlers query with LINQ directly: a repository layer would only re-wrap DbSet.
/// Both sets are already filtered to the current user by a global query filter.
/// </summary>
public interface IAppDbContext
{
    DbSet<Expense> Expenses { get; }
    DbSet<Budget> Budgets { get; }

    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;

    /// <exception cref="Exceptions.UniqueConstraintViolationException">A unique index rejected the write.</exception>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
