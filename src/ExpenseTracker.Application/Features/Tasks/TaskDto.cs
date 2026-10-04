using ExpenseTracker.Domain.Enums;
using ExpenseTracker.Domain.Tasks;

namespace ExpenseTracker.Application.Features.Tasks;

/// <param name="CanComplete">Computed on the server so the UI shows exactly the actions the API will accept.</param>
public sealed record TaskDto(
    Guid Id,
    string Title,
    ExpenseCategory Category,
    Guid AssigneeId,
    string AssigneeName,
    Guid CreatedById,
    string CreatedByName,
    DateOnly DueOn,
    TaskRecurrence Recurrence,
    DateTimeOffset? CompletedAt,
    string? CompletedByName,
    decimal? PaidAmount,
    bool CanComplete,
    bool CanDelete);

/// <param name="Done">The most recently finished tasks, newest first.</param>
public sealed record TaskListDto(IReadOnlyList<TaskDto> Open, IReadOnlyList<TaskDto> Done);

/// <summary>The result of completing a task: the finished task and, for a monthly task, next month's.</summary>
public sealed record CompleteTaskResult(TaskDto Completed, TaskDto? Next);
