using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;

namespace ExpenseTracker.Api.IntegrationTests;

/// <summary>
/// Runs the real API against a throwaway Postgres, so unique indexes, query filters and
/// migrations behave exactly as in production. Requires Docker. Models are replaced by deterministic fakes.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Overridable so CI or rate-limited machines can point at a registry mirror.
    private static readonly string Image =
        Environment.GetEnvironmentVariable("TEST_POSTGRES_IMAGE") ?? "pgvector/pgvector:pg17";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().WithImage(Image).Build();

    public ProgrammableChatClient Model { get; } = new();

    // Tests use fixed dates (September 2026); pinning the clock keeps "not in the future" rules stable as real time passes.
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 9, 30, 9, 0, 0, TimeSpan.Zero));

    public async Task InitializeAsync() => await _postgres.StartAsync();

    /// <summary>Writes an expense the way the app did before accounts existed: owned by a device id, in its own ledger.</summary>
    public async Task SeedDeviceExpenseAsync(Guid deviceId, decimal amount, string spentOn)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ExpenseTracker.Infrastructure.Persistence.AppDbContext>();
        db.Expenses.Add(ExpenseTracker.Domain.Entities.Expense.Create(
            deviceId, deviceId, amount, ExpenseTracker.Domain.Enums.ExpenseCategory.Food, "cihazdan", DateOnly.Parse(spentOn), null));
        await db.SaveChangesAsync(default);
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Ai:EmbeddingModel", "fake-embeddings");
        // Every test registers users from the same in-memory "IP"; production limits would throttle the suite.
        builder.UseSetting("RateLimits:PerMinute", "100000");
        // The fake's bag-of-words vectors score lower than a real model; the threshold is tuned for it.
        builder.UseSetting("Ai:MinSimilarity", "0.3");

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(Clock);
            services.AddSingleton<IChatClient>(Model);
            services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, FakeEmbeddingGenerator>();
        });
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
