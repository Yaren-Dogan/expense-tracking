using System.Security.Claims;
using ExpenseTracker.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Middleware;

/// <summary>
/// Decides once per request which ledger the signed-in user works in: their household's if they belong to one,
/// otherwise their personal ledger. Handlers and the ledger query filter read the result through IUserContext.
/// </summary>
public sealed class LedgerMiddleware(RequestDelegate next)
{
    public const string ItemKey = "LedgerId";

    public async Task InvokeAsync(HttpContext context, IAppDbContext db)
    {
        if (Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            var householdId = await db.Households
                .Where(h => h.Members.Any(m => m.UserId == userId))
                .Select(h => (Guid?)h.Id)
                .SingleOrDefaultAsync(context.RequestAborted);

            context.Items[ItemKey] = householdId ?? userId;
        }

        await next(context);
    }
}
