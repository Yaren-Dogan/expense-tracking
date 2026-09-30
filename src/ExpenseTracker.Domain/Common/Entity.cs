namespace ExpenseTracker.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7(); // time-ordered: keeps the primary-key index append-only

    // Set by the persistence layer from TimeProvider so the domain stays free of clocks.
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
}
