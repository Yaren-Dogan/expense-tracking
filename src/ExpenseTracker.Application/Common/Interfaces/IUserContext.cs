namespace ExpenseTracker.Application.Common.Interfaces;

public interface IUserContext
{
    /// <summary>The signed-in user. Guaranteed non-empty inside an authenticated request.</summary>
    Guid UserId { get; }

    /// <summary>
    /// The ledger this request reads and writes: the user's household if they are in one, otherwise their personal
    /// ledger (whose id equals <see cref="UserId"/>). Resolved once per request, before the handler runs.
    /// </summary>
    Guid LedgerId { get; }
}
