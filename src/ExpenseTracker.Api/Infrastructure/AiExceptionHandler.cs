using ExpenseTracker.Application.Common.Exceptions;
using System.ClientModel;
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
            AiUnavailableException e => (StatusCodes.Status503ServiceUnavailable, "AI service not configured.", e.Message),
            // Hosted providers (Gemini, OpenAI) report problems as HTTP statuses; tell the user which one it is.
            ClientResultException { Status: 401 or 403 } => (StatusCodes.Status503ServiceUnavailable, "AI service rejected the key.",
                "Yapay zekâ servisi API anahtarını kabul etmedi. .env dosyasındaki AI_API_KEY değerini kontrol et."),
            ClientResultException { Status: 429 } => (StatusCodes.Status503ServiceUnavailable, "AI quota exceeded.",
                "Yapay zekâ servisinin ücretsiz kotası şu an doldu. Bir dakika bekleyip tekrar dene."),
            ClientResultException { Status: 404 } => (StatusCodes.Status503ServiceUnavailable, "AI model not found.",
                "Yapay zekâ modeli bulunamadı. .env dosyasındaki CHAT_MODEL / EMBEDDING_MODEL adlarını kontrol et."),
            ClientResultException { Status: 500 or 502 or 503 or 504 } => (StatusCodes.Status503ServiceUnavailable, "AI provider overloaded.",
                "Yapay zekâ servisi şu an yoğun (sağlayıcı tarafında geçici bir sorun). Birkaç saniye sonra tekrar dene."),
            ClientResultException => (StatusCodes.Status503ServiceUnavailable, "AI service error.",
                "Yapay zekâ servisi şu an cevap vermiyor. Biraz sonra tekrar dene."),
            HttpRequestException { StatusCode: System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden } =>
                (StatusCodes.Status503ServiceUnavailable, "AI service rejected the key.",
                "Yapay zekâ servisi API anahtarını kabul etmedi. .env dosyasındaki AI_API_KEY değerini kontrol et."),
            HttpRequestException { StatusCode: System.Net.HttpStatusCode.TooManyRequests } =>
                (StatusCodes.Status503ServiceUnavailable, "AI quota exceeded.",
                "Yapay zekâ servisinin ücretsiz kotası şu an doldu. Bir dakika bekleyip tekrar dene."),
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
