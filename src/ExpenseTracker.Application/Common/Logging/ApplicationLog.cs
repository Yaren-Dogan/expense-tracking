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
}
