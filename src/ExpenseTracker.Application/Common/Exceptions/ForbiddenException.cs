namespace ExpenseTracker.Application.Common.Exceptions;

/// <summary>The user can see the resource but may not change it (e.g. deleting someone else's expense). Maps to 403.</summary>
public sealed class ForbiddenException(string message) : Exception(message);
