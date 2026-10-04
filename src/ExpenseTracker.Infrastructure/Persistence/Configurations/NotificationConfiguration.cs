using ExpenseTracker.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();

        builder.Property(n => n.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(n => n.Message).HasMaxLength(Notification.MessageMaxLength);

        builder.HasIndex(n => new { n.UserId, n.CreatedAt });
        // Each kind of notice about a task reaches a user once; the reminder job relies on it to stay idempotent.
        builder.HasIndex(n => new { n.UserId, n.SubjectId, n.Kind }).IsUnique();
    }
}
