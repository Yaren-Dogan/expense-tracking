using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Features.Assistant.Commands.AskAssistant;
using ExpenseTracker.Infrastructure.Ai;
using ExpenseTracker.Infrastructure.Persistence;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;
using ExpenseTracker.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ExpenseTracker.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Postgres";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.UseVector())
            .UseSnakeCaseNamingConvention());
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<AppClockOptions>()
            .Bind(configuration.GetSection(AppClockOptions.SectionName))
            .Validate(o => TimeZoneInfo.TryFindSystemTimeZoneById(o.TimeZone, out _), "Clock:TimeZone is not a known time zone.")
            .ValidateOnStart();
        services.AddSingleton<IAppClock, AppClock>();

        services.AddAi(configuration);

        return services;
    }

    /// <summary>
    /// Registers the model behind the provider-neutral Microsoft.Extensions.AI interfaces. Swapping Ollama for
    /// another provider (Claude, OpenAI, Azure) only changes the two client factories below.
    /// </summary>
    private static void AddAi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AiOptions>()
            .Bind(configuration.GetSection(AiOptions.SectionName))
            .Validate(o => Uri.TryCreate(o.Endpoint, UriKind.Absolute, out _), "Ai:Endpoint must be an absolute URL.")
            .ValidateOnStart();

        services.AddOptions<AssistantOptions>().Bind(configuration.GetSection(AssistantOptions.SectionName));

        services.AddHttpClient("ollama", (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<AiOptions>>().Value;
            client.BaseAddress = new Uri(options.Endpoint);
            client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
        });

        services.AddChatClient(sp =>
            {
                var options = sp.GetRequiredService<IOptions<AiOptions>>().Value;
                var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("ollama");
                return new OllamaApiClient(http, options.ChatModel);
            })
            .Use((inner, sp) => new FunctionInvokingChatClient(inner, sp.GetService<ILoggerFactory>(), sp)
            {
                MaximumIterationsPerRequest = sp.GetRequiredService<IOptions<AssistantOptions>>().Value.MaxToolIterations
            })
            .UseLogging();

        services.AddEmbeddingGenerator(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AiOptions>>().Value;
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("ollama");
            return new OllamaApiClient(http, options.EmbeddingModel);
        });

        services.AddScoped<ISemanticExpenseSearch, PgvectorExpenseSearch>();
        services.AddSingleton<ExpenseEmbeddingIndexer>();
        services.AddHostedService(sp => sp.GetRequiredService<ExpenseEmbeddingIndexer>());
    }
}
