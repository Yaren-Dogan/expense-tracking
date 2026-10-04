using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain.Households;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Features.Households;

internal static class HouseholdQueries
{
    public static Task<Household?> FindForUserAsync(this IAppDbContext db, Guid userId, CancellationToken ct) =>
        db.Households.Include(h => h.Members).SingleOrDefaultAsync(h => h.Members.Any(m => m.UserId == userId), ct);

    public static async Task<HouseholdDto> ToDtoAsync(
        this Household household, Guid currentUserId, IUserDirectory users, CancellationToken ct)
    {
        var names = await users.GetDisplayNamesAsync(household.Members.Select(m => m.UserId), ct);
        var members = household.Members
            .OrderBy(m => m.JoinedAt)
            .Select(m => new HouseholdMemberDto(
                m.UserId, names.GetValueOrDefault(m.UserId) ?? "?", m.Role, m.JoinedAt, m.UserId == currentUserId))
            .ToList();

        return new HouseholdDto(household.Id, household.Name, household.InviteCode, household.IsOwner(currentUserId), members);
    }
}
