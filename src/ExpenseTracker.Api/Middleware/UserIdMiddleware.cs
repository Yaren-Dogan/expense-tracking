using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Middleware;

/// <summary>
/// Every /api request must say whose data it touches. Rejecting here keeps handlers free of
/// "is there a user?" checks. MVP only: the id is an identifier, not a credential (see docs/prd.md).
/// </summary>
public sealed partial class UserIdMiddleware(RequestDelegate next, ILogger<UserIdMiddleware> logger)
{
    public const string HeaderName = "X-User-Id";
    public const string ItemKey = "UserId";

    public async Task InvokeAsync(HttpContext context, IProblemDetailsService problemDetails)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        var raw = context.Request.Headers[HeaderName].ToString();
        if (!Guid.TryParse(raw, out var userId) || userId == Guid.Empty)
        {
            LogInvalidUserHeader(logger, context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await problemDetails.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Missing or invalid user id",
                    Detail = $"The {HeaderName} header must be a non-empty GUID."
                }
            });
            return;
        }

        context.Items[ItemKey] = userId;
        await next(context);
    }

    [LoggerMessage(EventId = 2001, EventName = "InvalidUserHeader", Level = LogLevel.Warning,
        Message = "Rejected request to {Path}: missing or invalid user header")]
    private static partial void LogInvalidUserHeader(ILogger logger, string path);
}
