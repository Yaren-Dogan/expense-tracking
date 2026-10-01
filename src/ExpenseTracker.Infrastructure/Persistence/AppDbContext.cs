using ExpenseTracker.Application.Common.Exceptions;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain.Common;
using ExpenseTracker.Domain.Entities;
using ExpenseTracker.Infrastructure.Ai;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ExpenseTracker.Infrastructure.Persistence;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    IUserContext userContext,
    TimeProvider timeProvider)
    : DbContext(options), IAppDbContext
{
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Budget> Budgets => Set<Budget>();

    // Not on IAppDbContext: only Infrastructure's search and indexer touch the vector index.
    public DbSet<ExpenseEmbedding> ExpenseEmbeddings => Set<ExpenseEmbedding>();

    // Read through a context member so EF re-evaluates it per query instead of baking in a constant.
    private Guid CurrentUserId => userContext.UserId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Second line of defence for user isolation; handlers still filter by UserId explicitly.
        modelBuilder.Entity<Expense>().HasQueryFilter(e => e.UserId == CurrentUserId);
        modelBuilder.Entity<Budget>().HasQueryFilter(b => b.UserId == CurrentUserId);
        modelBuilder.Entity<ExpenseEmbedding>().HasQueryFilter(e => e.UserId == CurrentUserId);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAuditFields();

        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            throw new UniqueConstraintViolationException(pg.ConstraintName, ex);
        }
    }

    private void StampAuditFields()
    {
        // Postgres stores microseconds; truncating here keeps the value we return equal to the one we persist.
        var utcNow = timeProvider.GetUtcNow();
        var now = utcNow.AddTicks(-(utcNow.Ticks % TimeSpan.TicksPerMicrosecond));
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added)
                entry.Property(e => e.CreatedAt).CurrentValue = now;
            else if (entry.State == EntityState.Modified)
                entry.Property(e => e.UpdatedAt).CurrentValue = now;
        }
    }
}
