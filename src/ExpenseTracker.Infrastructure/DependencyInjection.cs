using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Features.Assistant.Commands.AskAssistant;
using ExpenseTracker.Infrastructure.Ai;
using ExpenseTracker.Infrastructure.Persistence;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OpenAI;
using System.ClientModel;
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
    /// Registers the model behind the provider-neutral Microsoft.Extensions.AI interfaces. Application code never sees
    /// which provider is configured (Ai:Provider): Ollama locally, or any OpenAI-compatible API such as Gemini.
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

        services.AddChatClient(CreateChatClient)
            .Use((inner, sp) => new FunctionInvokingChatClient(inner, sp.GetService<ILoggerFactory>(), sp)
            {
                MaximumIterationsPerRequest = sp.GetRequiredService<IOptions<AssistantOptions>>().Value.MaxToolIterations
            })
            .UseLogging();

        services.AddEmbeddingGenerator(sp =>
            new FixedDimensionEmbeddingGenerator(CreateEmbeddingGenerator(sp), AiOptions.EmbeddingDimensions));

        services.AddScoped<ISemanticExpenseSearch, PgvectorExpenseSearch>();
        services.AddSingleton<ExpenseEmbeddingIndexer>();
        services.AddHostedService(sp => sp.GetRequiredService<ExpenseEmbeddingIndexer>());
    }

    private static IChatClient CreateChatClient(IServiceProvider sp)
    {
        var options = sp.GetRequiredService<IOptions<AiOptions>>().Value;
        return options.Provider switch
        {
            AiProvider.Ollama => new OllamaApiClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("ollama"), options.ChatModel),
            _ when string.IsNullOrWhiteSpace(options.ApiKey) => new UnconfiguredAiClient(),
            _ => CreateOpenAIClient(options).GetChatClient(options.ChatModel).AsIChatClient()
        };
    }

    private static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(IServiceProvider sp)
    {
        var options = sp.GetRequiredService<IOptions<AiOptions>>().Value;
        return options.Provider switch
        {
            AiProvider.Ollama => new OllamaApiClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("ollama"), options.EmbeddingModel),
            _ when string.IsNullOrWhiteSpace(options.ApiKey) => new UnconfiguredAiClient(),
            _ => CreateOpenAIClient(options).GetEmbeddingClient(options.EmbeddingModel).AsIEmbeddingGenerator()
        };
    }

    /// <summary>One client type for every OpenAI-compatible provider; only the endpoint, key and model names differ.</summary>
    private static OpenAIClient CreateOpenAIClient(AiOptions options) =>
        new(new ApiKeyCredential(options.ApiKey!), new OpenAIClientOptions
        {
            Endpoint = new Uri(options.Endpoint),
            NetworkTimeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds)
        });
}
