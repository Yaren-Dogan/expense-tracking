using System.Security.Claims;
using ExpenseTracker.Api.Middleware;
using ExpenseTracker.Application.Common.Interfaces;

namespace ExpenseTracker.Api.Infrastructure;

public sealed class HttpUserContext(IHttpContextAccessor accessor) : IUserContext
{
    public Guid UserId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new InvalidOperationException("No signed-in user on this request.");

    public Guid LedgerId =>
        accessor.HttpContext?.Items[LedgerMiddleware.ItemKey] as Guid?
        ?? throw new InvalidOperationException("Ledger not resolved; is the request authenticated and after LedgerMiddleware?");
}
