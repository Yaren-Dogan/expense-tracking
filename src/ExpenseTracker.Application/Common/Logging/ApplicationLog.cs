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
        Message = "Budget for {Month} set to {Amount} in ledger {LedgerId}")]
    public static partial void BudgetSet(this ILogger logger, string month, decimal amount, Guid ledgerId);

    [LoggerMessage(EventId = 1005, EventName = "WeatherCalculated", Level = LogLevel.Debug,
        Message = "Weather for ledger {LedgerId} on {Date}: {Condition}")]
    public static partial void WeatherCalculated(this ILogger logger, Guid ledgerId, DateOnly date, WeatherCondition? condition);

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

    [LoggerMessage(EventId = 1301, EventName = "TaskCreated", Level = LogLevel.Information,
        Message = "Task {TaskId} created in ledger {LedgerId} for {AssigneeId}")]
    public static partial void TaskCreated(this ILogger logger, Guid taskId, Guid ledgerId, Guid assigneeId);

    [LoggerMessage(EventId = 1302, EventName = "TaskCompleted", Level = LogLevel.Information,
        Message = "Task {TaskId} completed by {UserId}, payment expense {ExpenseId}")]
    public static partial void TaskCompleted(this ILogger logger, Guid taskId, Guid userId, Guid? expenseId);

    [LoggerMessage(EventId = 1303, EventName = "TaskDeleted", Level = LogLevel.Information,
        Message = "Task {TaskId} deleted by {UserId}")]
    public static partial void TaskDeleted(this ILogger logger, Guid taskId, Guid userId);

    [LoggerMessage(EventId = 1304, EventName = "TaskRemindersSent", Level = LogLevel.Information,
        Message = "{Count} task reminders sent for {Today}")]
    public static partial void TaskRemindersSent(this ILogger logger, int count, DateOnly today);

    [LoggerMessage(EventId = 1305, EventName = "TasksHandedOver", Level = LogLevel.Information,
        Message = "{Count} open tasks of {LeaverId} handed to {OwnerId} in household {HouseholdId}")]
    public static partial void TasksHandedOver(this ILogger logger, int count, Guid leaverId, Guid ownerId, Guid householdId);
}
