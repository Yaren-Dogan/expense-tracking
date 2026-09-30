using ExpenseTracker.Api.Middleware;
using ExpenseTracker.Application.Common.Interfaces;

namespace ExpenseTracker.Api.Infrastructure;

public sealed class HttpUserContext(IHttpContextAccessor accessor) : IUserContext
{
    public Guid UserId =>
        accessor.HttpContext?.Items[UserIdMiddleware.ItemKey] as Guid?
        ?? throw new InvalidOperationException("No user on this request; is it outside /api or before UserIdMiddleware?");
}
