using ExpenseTracker.Domain.Insights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure.Persistence.Configurations;

public sealed class MonthReviewConfiguration : IEntityTypeConfiguration<MonthReview>
{
    public void Configure(EntityTypeBuilder<MonthReview> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Headline).HasMaxLength(MonthReview.HeadlineMaxLength);
        builder.Property(r => r.Tip).HasMaxLength(MonthReview.TipMaxLength);
        builder.Property(r => r.GeneratedBy).HasMaxLength(100);
        // Stored as a text[] column; the entity exposes it read-only.
        builder.Ignore(r => r.Highlights);
        builder.Property<string[]>("_highlights").HasColumnName("highlights");
        builder.Ignore(r => r.Period);

        // One review per ledger and month: the background job and a user request cannot both create one.
        builder.HasIndex(r => new { r.LedgerId, r.Year, r.Month }).IsUnique();
    }
}
