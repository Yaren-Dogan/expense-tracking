namespace ExpenseTracker.Application.Common.Exceptions;

/// <summary>The request is valid but clashes with the current state (e.g. already in a household). Maps to 409.</summary>
public sealed class ConflictException(string message) : Exception(message);
