using ExpenseTracker.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure.Persistence.Configurations;

public sealed class HouseholdTaskConfiguration : IEntityTypeConfiguration<HouseholdTask>
{
    public void Configure(EntityTypeBuilder<HouseholdTask> builder)
    {
        builder.ToTable("tasks");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Title).HasMaxLength(HouseholdTask.TitleMaxLength);
        builder.Property(t => t.Category).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Recurrence).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.PaidAmount).HasPrecision(12, 2);
        builder.Ignore(t => t.IsDone);

        builder.HasIndex(t => new { t.LedgerId, t.CompletedAt, t.DueOn });
        // One occurrence per month of a series, even if two people complete the previous one at the same moment.
        builder.HasIndex(t => new { t.SeriesId, t.DueOn }).IsUnique();
        // The reminder job scans open tasks by due date across all ledgers.
        builder.HasIndex(t => t.DueOn).HasFilter("completed_at IS NULL");
    }
}
