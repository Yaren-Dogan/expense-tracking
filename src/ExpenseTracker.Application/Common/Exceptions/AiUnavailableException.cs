namespace ExpenseTracker.Application.Common.Exceptions;

/// <summary>The model provider cannot serve requests right now (not configured, bad key, quota, outage). Maps to 503.</summary>
public sealed class AiUnavailableException(string message, Exception? innerException = null) : Exception(message, innerException);
