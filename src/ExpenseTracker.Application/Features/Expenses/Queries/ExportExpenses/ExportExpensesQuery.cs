using MediatR;

namespace ExpenseTracker.Application.Features.Expenses.Queries.ExportExpenses;

/// <summary>The active ledger's expenses for one month as a CSV file Excel opens directly.</summary>
/// <param name="Month"><c>yyyy-MM</c>.</param>
public sealed record ExportExpensesQuery(string Month) : IRequest<ExpenseExport>;

public sealed record ExpenseExport(string FileName, byte[] Content);
