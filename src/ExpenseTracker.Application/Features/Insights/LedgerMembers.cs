using ExpenseTracker.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Features.Insights;

internal static class LedgerMembers
{
    /// <summary>
    /// The people who share a ledger: the household's members when the ledger is a household, otherwise its owner.
    /// Empty for a ledger nobody can sign in to (data recorded under a device id before accounts existed).
    /// </summary>
    public static async Task<IReadOnlyList<Guid>> ResolveAsync(
        IAppDbContext db, IUserDirectory users, Guid ledgerId, CancellationToken ct)
    {
        var household = await db.Households.AsNoTracking().Include(h => h.Members)
            .SingleOrDefaultAsync(h => h.Id == ledgerId, ct);
        if (household is not null)
            return household.Members.Select(m => m.UserId).ToList();

        return await users.ExistsAsync(ledgerId, ct) ? [ledgerId] : [];
    }
}
