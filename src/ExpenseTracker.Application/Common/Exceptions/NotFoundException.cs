namespace ExpenseTracker.Application.Common.Exceptions;

/// <summary>Maps to 404.</summary>
public sealed class NotFoundException(string message) : Exception(message);
