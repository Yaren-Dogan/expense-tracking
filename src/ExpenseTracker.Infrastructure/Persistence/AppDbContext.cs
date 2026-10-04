using ExpenseTracker.Application.Common.Exceptions;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain.Common;
using ExpenseTracker.Domain.Entities;
using ExpenseTracker.Domain.Households;
using ExpenseTracker.Domain.Insights;
using ExpenseTracker.Domain.Notifications;
using ExpenseTracker.Domain.Tasks;
using ExpenseTracker.Infrastructure.Ai;
using ExpenseTracker.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ExpenseTracker.Infrastructure.Persistence;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    IUserContext userContext,
    TimeProvider timeProvider)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options), IAppDbContext, IDataProtectionKeyContext
{
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdTask> Tasks => Set<HouseholdTask>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<MonthReview> MonthReviews => Set<MonthReview>();
    public DbSet<BudgetPlan> BudgetPlans => Set<BudgetPlan>();

    // Keys that encrypt sign-in cookies. In the database (not the container's disk) so a rebuild does not sign everyone out.
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    // Not on IAppDbContext: only Infrastructure's search and indexer touch the vector index.
    public DbSet<ExpenseEmbedding> ExpenseEmbeddings => Set<ExpenseEmbedding>();

    // Read through a context member so EF re-evaluates it per query instead of baking in a constant.
    private Guid CurrentLedgerId => userContext.LedgerId;
    private Guid CurrentUserId => userContext.UserId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identity's tables (users, logins, tokens) first; snake_case naming applies to them too.
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Second line of defence for ledger isolation; handlers still filter by LedgerId explicitly.
        // Embeddings are not filtered here: search always joins them to (filtered) expenses.
        modelBuilder.Entity<Expense>().HasQueryFilter(e => e.LedgerId == CurrentLedgerId);
        modelBuilder.Entity<Budget>().HasQueryFilter(b => b.LedgerId == CurrentLedgerId);
        modelBuilder.Entity<HouseholdTask>().HasQueryFilter(t => t.LedgerId == CurrentLedgerId);
        modelBuilder.Entity<Notification>().HasQueryFilter(n => n.UserId == CurrentUserId);
        modelBuilder.Entity<MonthReview>().HasQueryFilter(r => r.LedgerId == CurrentLedgerId);
        modelBuilder.Entity<BudgetPlan>().HasQueryFilter(p => p.LedgerId == CurrentLedgerId);

        SchemaComments.Apply(modelBuilder);
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
