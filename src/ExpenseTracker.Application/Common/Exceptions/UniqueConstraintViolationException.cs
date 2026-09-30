namespace ExpenseTracker.Application.Common.Exceptions;

/// <summary>
/// Provider-neutral signal that a unique index rejected a write, so handlers can recover from races
/// without catching every <c>DbUpdateException</c>.
/// </summary>
public sealed class UniqueConstraintViolationException(string? constraintName, Exception innerException)
    : Exception($"Unique constraint '{constraintName}' was violated.", innerException)
{
    public string? ConstraintName { get; } = constraintName;
}
