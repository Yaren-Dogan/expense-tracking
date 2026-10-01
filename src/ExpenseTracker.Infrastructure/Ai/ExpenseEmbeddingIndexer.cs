using ExpenseTracker.Application.Common.Ai;
using ExpenseTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;

namespace ExpenseTracker.Infrastructure.Ai;

/// <summary>
/// Keeps the vector index in sync in the background. Saving an expense never waits for (or fails because of)
/// the model: missing or outdated vectors are filled in on the next tick, and an unreachable model is retried.
/// </summary>
public sealed partial class ExpenseEmbeddingIndexer(
    IServiceScopeFactory scopeFactory,
    IOptions<AiOptions> options,
    TimeProvider timeProvider,
    ILogger<ExpenseEmbeddingIndexer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(options.Value.IndexIntervalSeconds);
        using var timer = new PeriodicTimer(interval, timeProvider);

        do
        {
            try
            {
                // Drain the backlog before waiting again.
                while (await IndexBatchAsync(stoppingToken) > 0) { }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Ollama is up but the model is not pulled yet (normal on first start): one line, no stack trace.
                LogModelNotAvailable(logger, options.Value.EmbeddingModel);
            }
            catch (HttpRequestException ex) when (ex.StatusCode is null)
            {
                LogModelServerUnreachable(logger, options.Value.Endpoint);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogIndexingFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task<int> IndexBatchAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var generator = scope.ServiceProvider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        var model = options.Value.EmbeddingModel;

        // Background work spans all users, and there is no HTTP user here, so the user filters are bypassed on purpose.
        var pending = await db.Expenses.IgnoreQueryFilters()
            .Where(e => !db.ExpenseEmbeddings.IgnoreQueryFilters().Any(v => v.ExpenseId == e.Id && v.Model == model))
            .OrderBy(e => e.CreatedAt)
            .Take(options.Value.IndexBatchSize)
            .Select(e => new { e.Id, e.UserId, e.Category, e.Note })
            .ToListAsync(ct);

        if (pending.Count == 0)
            return 0;

        var vectors = await generator.GenerateAsync(
            pending.Select(e => CategoryNames.EmbeddingText(e.Category, e.Note)), cancellationToken: ct);

        var ids = pending.Select(e => e.Id).ToList();
        var existing = await db.ExpenseEmbeddings.IgnoreQueryFilters()
            .Where(v => ids.Contains(v.ExpenseId))
            .ToDictionaryAsync(v => v.ExpenseId, ct);

        var now = timeProvider.GetUtcNow();
        foreach (var (expense, vector) in pending.Zip(vectors))
        {
            if (!existing.TryGetValue(expense.Id, out var row))
            {
                row = new ExpenseEmbedding { ExpenseId = expense.Id, UserId = expense.UserId };
                db.ExpenseEmbeddings.Add(row);
            }

            row.Embedding = new Vector(vector.Vector);
            row.Model = model;
            row.IndexedAt = now;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.ForeignKeyViolation })
        {
            // An expense was deleted between reading and writing; the next tick simply skips it.
            LogExpenseDeletedDuringIndexing(logger);
            return 0;
        }

        LogIndexed(logger, pending.Count, model);
        return pending.Count;
    }

    [LoggerMessage(EventId = 3001, EventName = "ExpensesIndexed", Level = LogLevel.Information,
        Message = "Indexed {Count} expenses with {Model}")]
    private static partial void LogIndexed(ILogger logger, int count, string model);

    [LoggerMessage(EventId = 3002, EventName = "IndexingFailed", Level = LogLevel.Warning,
        Message = "Embedding indexing failed; will retry on the next tick")]
    private static partial void LogIndexingFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3004, EventName = "EmbeddingModelNotAvailable", Level = LogLevel.Warning,
        Message = "Embedding model '{Model}' is not available in Ollama yet (still downloading?); will retry")]
    private static partial void LogModelNotAvailable(ILogger logger, string model);

    [LoggerMessage(EventId = 3005, EventName = "ModelServerUnreachable", Level = LogLevel.Warning,
        Message = "Model server {Endpoint} is unreachable; will retry")]
    private static partial void LogModelServerUnreachable(ILogger logger, string endpoint);

    [LoggerMessage(EventId = 3003, EventName = "ExpenseDeletedDuringIndexing", Level = LogLevel.Debug,
        Message = "An expense was deleted while being indexed; batch will be retried")]
    private static partial void LogExpenseDeletedDuringIndexing(ILogger logger);
}
