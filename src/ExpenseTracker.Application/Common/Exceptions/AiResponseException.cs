namespace ExpenseTracker.Application.Common.Exceptions;

/// <summary>The model answered, but not with something we can use. Maps to 422.</summary>
public sealed class AiResponseException(string message) : Exception(message);
