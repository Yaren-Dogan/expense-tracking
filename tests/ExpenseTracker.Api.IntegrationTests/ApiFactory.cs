using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MediatR;
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

    /// <summary>Runs the reminder job once, as its background scheduler would (the scheduler is off in tests).</summary>
    public async Task<int> RunTaskRemindersAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new ExpenseTracker.Application.Features.Tasks.Commands.SendTaskReminders.SendTaskRemindersCommand());
    }

    /// <summary>Runs the monthly review job once, as its background scheduler would (off in tests).</summary>
    public async Task<int> RunMonthReviewsAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new ExpenseTracker.Application.Features.Insights.Reviews.Commands.GenerateDueMonthReviews.GenerateDueMonthReviewsCommand());
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
        builder.UseSetting("RateLimits:AiPerMinute", "100000");
        // The fake's bag-of-words vectors score lower than a real model; the threshold is tuned for it.
        builder.UseSetting("Ai:MinSimilarity", "0.3");
        // The shared database holds every test's ledgers; the monthly job must reach this test's one in a single run.
        builder.UseSetting("Insights:ReviewBatchSize", "500");

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(Clock);
            // The session cookie is persistent and expires 14 days after the pinned clock, but the test client's cookie
            // container checks expiry against the real date. Once the real date passed that point every request after
            // sign-in was 401. The cookie lives on real time; the business rules keep the pinned date.
            services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme,
                options => options.TimeProvider = TimeProvider.System);
            // Tests trigger reminders explicitly, so a background run can never race an assertion.
            Type[] schedulers = [typeof(ExpenseTracker.Infrastructure.Tasks.TaskReminderScheduler), typeof(ExpenseTracker.Infrastructure.Insights.MonthReviewScheduler)];
            foreach (var scheduler in services.Where(d => d.ServiceType == typeof(IHostedService) && schedulers.Contains(d.ImplementationType)).ToList())
                services.Remove(scheduler);
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
