using ExpenseTracker.Domain.Households;
using ExpenseTracker.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure.Persistence.Configurations;

public sealed class HouseholdConfiguration : IEntityTypeConfiguration<Household>
{
    public void Configure(EntityTypeBuilder<Household> builder)
    {
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();
        builder.Property(h => h.Name).HasMaxLength(Household.NameMaxLength);
        builder.Property(h => h.InviteCode).HasMaxLength(InviteCode.Length);
        builder.HasIndex(h => h.InviteCode).IsUnique();

        builder.HasMany(h => h.Members).WithOne().HasForeignKey(m => m.HouseholdId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(h => h.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class HouseholdMemberConfiguration : IEntityTypeConfiguration<HouseholdMember>
{
    public void Configure(EntityTypeBuilder<HouseholdMember> builder)
    {
        builder.HasKey(m => new { m.HouseholdId, m.UserId });
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(10);

        // One household per user, enforced by the database so two concurrent joins cannot both succeed.
        builder.HasIndex(m => m.UserId).IsUnique();
        builder.HasOne<AppUser>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
