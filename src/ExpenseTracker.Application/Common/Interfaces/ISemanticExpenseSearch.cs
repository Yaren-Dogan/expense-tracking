using ExpenseTracker.Application.Features.Expenses;

namespace ExpenseTracker.Application.Common.Interfaces;

/// <summary>RAG retrieval: finds a ledger's expenses whose meaning is close to a free-text query.</summary>
public interface ISemanticExpenseSearch
{
    Task<IReadOnlyList<ExpenseMatch>> SearchAsync(Guid ledgerId, string query, int top, CancellationToken cancellationToken);
}

/// <param name="Similarity">Cosine similarity, 1 = identical meaning.</param>
public sealed record ExpenseMatch(ExpenseDto Expense, double Similarity);
