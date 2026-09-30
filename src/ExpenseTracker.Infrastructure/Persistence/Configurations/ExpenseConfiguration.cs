using ExpenseTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure.Persistence.Configurations;

public sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Amount).HasPrecision(12, 2);
        // Stored as text so the table stays readable and reordering the enum cannot corrupt data.
        builder.Property(e => e.Category).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Note).HasMaxLength(Expense.NoteMaxLength);
        builder.Property(e => e.IdempotencyKey).HasMaxLength(Expense.IdempotencyKeyMaxLength);

        builder.HasIndex(e => new { e.UserId, e.SpentOn });
        builder.HasIndex(e => new { e.UserId, e.IdempotencyKey }).IsUnique();
    }
}
