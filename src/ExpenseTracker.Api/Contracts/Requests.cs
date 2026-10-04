using ExpenseTracker.Application.Features.Assistant.Commands.AskAssistant;
using ExpenseTracker.Domain.Enums;

namespace ExpenseTracker.Api.Contracts;

// HTTP bodies are kept separate from commands so header- and route-bound values
// (idempotency key, month) cannot be spoofed through the JSON body.
public sealed record CreateExpenseRequest(decimal Amount, ExpenseCategory Category, string? Note, DateOnly SpentOn);

public sealed record SetBudgetRequest(decimal Amount);

public sealed record ParseExpenseRequest(string Text, DateOnly? Today);

public sealed record AssistantChatRequest(IReadOnlyList<ChatTurn> Messages, DateOnly? Today);

public sealed record RegisterRequest(string Email, string Password, string DisplayName, Guid? DeviceUserId);

public sealed record LoginRequest(string Email, string Password, Guid? DeviceUserId);

public sealed record MeResponse(Guid Id, string Email, string DisplayName, HouseholdSummary? Household, int ClaimedExpenses = 0);

public sealed record HouseholdSummary(Guid Id, string Name);

public sealed record CreateHouseholdRequest(string Name);

public sealed record JoinHouseholdRequest(string Code);

public sealed record CreateTaskRequest(
    string Title,
    Guid AssigneeId,
    DateOnly DueOn,
    ExpenseTracker.Domain.Tasks.TaskRecurrence Recurrence = ExpenseTracker.Domain.Tasks.TaskRecurrence.None,
    ExpenseCategory Category = ExpenseCategory.Bills);

/// <param name="Amount">Optional: when given, the payment is also recorded as an expense.</param>
public sealed record CompleteTaskRequest(decimal? Amount);

public sealed record CreateBudgetPlanRequest(decimal GoalAmount, int Months, string? Purpose);
