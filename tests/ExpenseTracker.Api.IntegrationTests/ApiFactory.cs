using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
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

    public async Task InitializeAsync() => await _postgres.StartAsync();

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
        builder.UseSetting("Ai:IndexIntervalSeconds", "1");
        builder.UseSetting("Ai:EmbeddingModel", "fake-embeddings");
        // The fake's bag-of-words vectors score lower than a real model; the threshold is tuned for it.
        builder.UseSetting("Ai:MinSimilarity", "0.3");

        builder.ConfigureTestServices(services =>
        {
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
