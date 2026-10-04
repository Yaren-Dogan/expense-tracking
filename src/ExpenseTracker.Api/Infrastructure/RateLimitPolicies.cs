using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ExpenseTracker.Api;

public static class RateLimitPolicies
{
    /// <summary>Register/login, per client IP: slows password guessing beyond the account lockout.</summary>
    public const string Auth = "auth";

    /// <summary>Invite preview/join, per user: makes guessing invite codes impractical.</summary>
    public const string Invites = "invites";

    /// <summary>Endpoints that call the model, per user: one user cannot use up the provider quota everyone shares.</summary>
    public const string Ai = "ai";

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var perMinute = configuration.GetValue("RateLimits:PerMinute", 10);
        var aiPerMinute = configuration.GetValue("RateLimits:AiPerMinute", 20);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Auth, context => FixedWindow(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", perMinute));
            options.AddPolicy(Invites, context => FixedWindow(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous", perMinute));
            options.AddPolicy(Ai, context => FixedWindow(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous", aiPerMinute));
            options.OnRejected = async (context, ct) =>
            {
                await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests",
                    Detail = "Çok fazla deneme yapıldı. Bir dakika sonra tekrar dene."
                }, ct);
            };
        });

        return services;
    }

    private static RateLimitPartition<string> FixedWindow(string key, int perMinute) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = perMinute,
            Window = TimeSpan.FromMinutes(1)
        });
}
