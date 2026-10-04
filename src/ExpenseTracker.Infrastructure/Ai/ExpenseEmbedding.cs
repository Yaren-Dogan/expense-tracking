using Pgvector;

namespace ExpenseTracker.Infrastructure.Ai;

/// <summary>
/// Vector-index row for one expense. Kept out of the domain model: it is a derived, rebuildable search artefact,
/// not business data, and the domain should not depend on pgvector.
/// </summary>
public sealed class ExpenseEmbedding
{
    public Guid ExpenseId { get; set; }
    public Vector Embedding { get; set; } = null!;

    /// <summary>Which model produced the vector; rows from another model are re-embedded.</summary>
    public string Model { get; set; } = string.Empty;

    public DateTimeOffset IndexedAt { get; set; }
}
