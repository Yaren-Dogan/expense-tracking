using ExpenseTracker.Domain.Enums;
using MediatR;

namespace ExpenseTracker.Application.Features.Expenses.Queries.ParseExpense;

/// <summary>Turns a sentence into an expense suggestion. Nothing is saved: the user confirms in the form.</summary>
/// <param name="Today">The client's local date, so "dün" means the user's yesterday.</param>
public sealed record ParseExpenseQuery(string Text, DateOnly? Today) : IRequest<ParsedExpenseDto>;

public sealed record ParsedExpenseDto(decimal Amount, ExpenseCategory Category, string? Note, DateOnly SpentOn);
