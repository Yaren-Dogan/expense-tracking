using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace ExpenseTracker.Api.IntegrationTests;

/// <summary>
/// Runs the real API against a throwaway Postgres, so unique indexes, query filters and
/// migrations behave exactly as in production. Requires Docker.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Overridable so CI or rate-limited machines can point at a registry mirror.
    private static readonly string Image =
        Environment.GetEnvironmentVariable("TEST_POSTGRES_IMAGE") ?? "postgres:17-alpine";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().WithImage(Image).Build();

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
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
