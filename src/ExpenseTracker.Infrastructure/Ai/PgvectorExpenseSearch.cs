using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Features.Expenses;
using ExpenseTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace ExpenseTracker.Infrastructure.Ai;

/// <summary>Embeds the query with the same model as the index, then ranks the user's expenses by cosine distance in Postgres.</summary>
public sealed class PgvectorExpenseSearch(
    AppDbContext db,
    IEmbeddingGenerator<string, Embedding<float>> generator,
    IOptions<AiOptions> options) : ISemanticExpenseSearch
{
    public async Task<IReadOnlyList<ExpenseMatch>> SearchAsync(
        Guid ledgerId, string query, int top, CancellationToken cancellationToken)
    {
        var embedding = await generator.GenerateAsync(query, cancellationToken: cancellationToken);
        var queryVector = new Vector(embedding.Vector);
        var model = options.Value.EmbeddingModel;
        var maxDistance = 1 - options.Value.EffectiveMinSimilarity;

        // Filter to the ledger before ranking: taking the top k across everyone first would drop this ledger's matches.
        // The explicit LedgerId condition is the primary isolation; the expenses' global filter is the backstop.
        var rows = await db.ExpenseEmbeddings
            .Where(v => v.Model == model)
            .Join(db.Expenses.Where(e => e.LedgerId == ledgerId), v => v.ExpenseId, e => e.Id,
                (v, e) => new { Expense = e, Distance = v.Embedding.CosineDistance(queryVector) })
            .Where(x => x.Distance <= maxDistance)
            .OrderBy(x => x.Distance)
            .Take(top)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new ExpenseMatch(ExpenseDto.From(r.Expense), Math.Round(1 - r.Distance, 3)))
            .ToList();
    }
}
