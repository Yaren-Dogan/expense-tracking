namespace ExpenseTracker.Domain.Common;

/// <summary>Raised when an entity invariant would be broken. Validators should catch these cases first.</summary>
public sealed class DomainException(string message) : Exception(message);
