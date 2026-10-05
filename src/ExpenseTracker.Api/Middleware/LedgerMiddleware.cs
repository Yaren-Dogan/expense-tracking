using System.Security.Claims;
using ExpenseTracker.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Middleware;

/// <summary>
/// Decides once per request which ledger the signed-in user works in. A household member works in the household's
/// ledger by default and in their personal ledger when the client asks for it with <c>X-Ledger: personal</c>
/// (the ledger switcher; the choice is kept per device). Someone without a household always works in their personal
/// ledger. Handlers and the ledger query filter read the result through IUserContext.
/// </summary>
public sealed class LedgerMiddleware(RequestDelegate next)
{
    public const string ItemKey = "LedgerId";
    public const string HeaderName = "X-Ledger";
    public const string Personal = "personal";
    public const string Household = "household";

    public async Task InvokeAsync(HttpContext context, IAppDbContext db)
    {
        if (Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            // The personal ledger's id is the user's own id, so asking for it can never reach anyone else's data;
            // the household is still looked up from membership, never taken from the request.
            var householdId = WantsPersonal(context)
                ? null
                : await db.Households
                    .Where(h => h.Members.Any(m => m.UserId == userId))
                    .Select(h => (Guid?)h.Id)
                    .SingleOrDefaultAsync(context.RequestAborted);

            context.Items[ItemKey] = householdId ?? userId;
        }

        await next(context);
    }

    /// <summary>Unknown values are ignored, so a stale or mistyped header falls back to the default ledger.</summary>
    public static bool WantsPersonal(HttpContext context) =>
        string.Equals(context.Request.Headers[HeaderName].ToString().Trim(), Personal, StringComparison.OrdinalIgnoreCase);
}
