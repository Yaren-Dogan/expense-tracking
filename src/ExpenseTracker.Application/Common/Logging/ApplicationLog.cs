using ExpenseTracker.Domain.Enums;
using ExpenseTracker.Domain.Weather;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Application.Common.Logging;

/// <summary>Event names come from docs/prd.md "Loglama olayları"; ids are stable so dashboards can filter on them.</summary>
internal static partial class ApplicationLog
{
    [LoggerMessage(EventId = 1001, EventName = "ExpenseCreated", Level = LogLevel.Information,
        Message = "Expense {ExpenseId} created for user {UserId}: {Amount} on {SpentOn}")]
    public static partial void ExpenseCreated(this ILogger logger, Guid expenseId, Guid userId, decimal amount, DateOnly spentOn);

    [LoggerMessage(EventId = 1002, EventName = "ExpenseDuplicateRequest", Level = LogLevel.Information,
        Message = "Idempotent replay for user {UserId}, returning expense {ExpenseId}")]
    public static partial void ExpenseDuplicateRequest(this ILogger logger, Guid userId, Guid expenseId);

    [LoggerMessage(EventId = 1003, EventName = "ExpenseDeleted", Level = LogLevel.Information,
        Message = "Expense {ExpenseId} deleted for user {UserId}")]
    public static partial void ExpenseDeleted(this ILogger logger, Guid expenseId, Guid userId);

    [LoggerMessage(EventId = 1004, EventName = "BudgetSet", Level = LogLevel.Information,
        Message = "Budget for {Month} set to {Amount} for user {UserId}")]
    public static partial void BudgetSet(this ILogger logger, string month, decimal amount, Guid userId);

    [LoggerMessage(EventId = 1005, EventName = "WeatherCalculated", Level = LogLevel.Debug,
        Message = "Weather for user {UserId} on {Date}: {Condition}")]
    public static partial void WeatherCalculated(this ILogger logger, Guid userId, DateOnly date, WeatherCondition? condition);

    [LoggerMessage(EventId = 1101, EventName = "ExpenseParsed", Level = LogLevel.Information,
        Message = "Parsed expense as {Category} in {ElapsedMs} ms")]
    public static partial void ExpenseParsed(this ILogger logger, ExpenseCategory category, long elapsedMs);

    // Only the length is logged: the sentence itself is user content and stays out of logs.
    [LoggerMessage(EventId = 1102, EventName = "ExpenseParseFailed", Level = LogLevel.Warning,
        Message = "Model output for a {TextLength}-character sentence was unusable ({ElapsedMs} ms)")]
    public static partial void ExpenseParseFailed(this ILogger logger, int textLength, long elapsedMs);

    [LoggerMessage(EventId = 1201, EventName = "AssistantAnswered", Level = LogLevel.Information,
        Message = "Assistant answered user {UserId} in {ElapsedMs} ms using tools [{Tools}]")]
    public static partial void AssistantAnswered(this ILogger logger, Guid userId, long elapsedMs, string tools);
}
