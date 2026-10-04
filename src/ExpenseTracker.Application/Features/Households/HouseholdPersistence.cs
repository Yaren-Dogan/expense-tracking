using ExpenseTracker.Application.Common.Exceptions;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain.Households;

namespace ExpenseTracker.Application.Features.Households;

internal static class HouseholdPersistence
{
    private const int CodeAttempts = 3;

    /// <summary>
    /// Saves a household whose invite code may (very rarely) collide with another's, and turns a membership
    /// collision — the same user joining twice at once — into a 409.
    /// </summary>
    public static async Task SaveWithUniqueRetryAsync(this IAppDbContext db, Household household, Guid userId, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (UniqueConstraintViolationException ex) when (ex.ConstraintName?.Contains("invite_code") == true && attempt < CodeAttempts)
            {
                household.ReplaceInviteCode(household.Members.First(m => m.Role == HouseholdRole.Owner).UserId, InviteCode.New());
            }
            catch (UniqueConstraintViolationException ex) when (ex.ConstraintName?.Contains("user_id") == true)
            {
                throw new ConflictException("Zaten bir hanedesin.");
            }
        }
    }
}
