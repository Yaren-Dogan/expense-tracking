using ExpenseTracker.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Infrastructure;

/// <summary>
/// Model failures are expected operating conditions, not server bugs: an unusable answer is 422,
/// an unreachable or slow model is 503 so the client can say "try again" instead of "something broke".
/// </summary>
public sealed class AiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title, detail) = exception switch
        {
            AiResponseException e => (StatusCodes.Status422UnprocessableEntity, "Model output could not be used.", e.Message),
            HttpRequestException => (StatusCodes.Status503ServiceUnavailable, "AI service unavailable.",
                "Yapay zekâ servisine şu an ulaşılamıyor. Model indiriliyor olabilir; biraz sonra tekrar dene."),
            // A timeout surfaces as a cancellation the client did not ask for.
            TaskCanceledException when !context.RequestAborted.IsCancellationRequested =>
                (StatusCodes.Status503ServiceUnavailable, "AI service timed out.",
                "Yapay zekâ cevabı çok uzun sürdü. Biraz sonra tekrar dene."),
            _ => (0, "", "")
        };

        if (status == 0)
            return false;

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail }
        });
    }
}
