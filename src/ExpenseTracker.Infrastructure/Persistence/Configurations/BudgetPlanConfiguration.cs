using System.Text.Json;
using System.Text.Json.Serialization;
using ExpenseTracker.Domain.Insights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure.Persistence.Configurations;

public sealed class BudgetPlanConfiguration : IEntityTypeConfiguration<BudgetPlan>
{
    // Enum names, not numbers, so the stored JSON stays readable in Adminer and survives reordering the enums.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public void Configure(EntityTypeBuilder<BudgetPlan> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Purpose).HasMaxLength(BudgetPlan.PurposeMaxLength);
        builder.Property(p => p.Advice).HasMaxLength(BudgetPlan.AdviceMaxLength);
        builder.Property(p => p.GeneratedBy).HasMaxLength(100);
        builder.Property(p => p.GoalAmount).HasPrecision(12, 2);
        builder.Property(p => p.BaselineMonthly).HasPrecision(12, 2);
        builder.Property(p => p.MonthlyBudget).HasPrecision(12, 2);

        // The cuts are part of the plan and never queried on their own: one jsonb column.
        builder.Property(p => p.Cuts)
            .HasColumnType("jsonb")
            .HasConversion(
                cuts => JsonSerializer.Serialize(cuts, Json),
                json => JsonSerializer.Deserialize<List<PlanCut>>(json, Json) ?? new List<PlanCut>(),
                new ValueComparer<IReadOnlyList<PlanCut>>(
                    (a, b) => a!.SequenceEqual(b!),
                    cuts => cuts.Aggregate(0, (hash, cut) => HashCode.Combine(hash, cut)),
                    cuts => cuts.ToList()));

        builder.Ignore(p => p.Start);
        builder.Ignore(p => p.IsExtended);
        builder.Ignore(p => p.IsApplied);

        builder.HasIndex(p => new { p.LedgerId, p.CreatedAt });
    }
}
