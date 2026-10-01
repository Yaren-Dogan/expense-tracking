using ExpenseTracker.Domain.Entities;
using ExpenseTracker.Infrastructure.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseTracker.Infrastructure.Persistence.Configurations;

public sealed class ExpenseEmbeddingConfiguration : IEntityTypeConfiguration<ExpenseEmbedding>
{
    public void Configure(EntityTypeBuilder<ExpenseEmbedding> builder)
    {
        builder.HasKey(e => e.ExpenseId);
        builder.Property(e => e.Embedding).HasColumnType($"vector({AiOptions.EmbeddingDimensions})");
        builder.Property(e => e.Model).HasMaxLength(100);

        // Deleting an expense removes its vector, so search can never return a deleted expense.
        builder.HasOne<Expense>().WithOne().HasForeignKey<ExpenseEmbedding>(e => e.ExpenseId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.UserId);
        // HNSW keeps nearest-neighbour search fast as the table grows; cosine matches how similarity is scored.
        builder.HasIndex(e => e.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");
    }
}
