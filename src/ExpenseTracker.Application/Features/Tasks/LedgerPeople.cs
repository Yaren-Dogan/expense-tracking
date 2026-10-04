using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Features.Households;
using ExpenseTracker.Domain.Tasks;

namespace ExpenseTracker.Application.Features.Tasks;

/// <summary>
/// Who shares the current ledger: the household's members, or just the user in their personal ledger.
/// Tasks can only be given to these people, and the household owner may delete any task.
/// </summary>
internal sealed record LedgerPeople(IReadOnlySet<Guid> Members, Guid? OwnerId, IReadOnlyDictionary<Guid, string> Names)
{
    public static async Task<LedgerPeople> LoadAsync(
        IAppDbContext db, IUserContext user, IUserDirectory users, CancellationToken ct, IEnumerable<Guid>? alsoName = null)
    {
        var household = await db.FindForUserAsync(user.UserId, ct);
        var members = household?.Members.Select(m => m.UserId).ToHashSet() ?? [user.UserId];
        var ownerId = household?.Members.SingleOrDefault(m => household.IsOwner(m.UserId))?.UserId;

        // Former members still appear on old tasks, so their names are looked up too.
        var names = await users.GetDisplayNamesAsync(members.Concat(alsoName ?? []).Distinct(), ct);
        return new LedgerPeople(members, ownerId, names);
    }

    public string NameOf(Guid userId) => Names.GetValueOrDefault(userId) ?? "Eski üye";

    public bool CanDelete(HouseholdTask task, Guid userId) => task.CreatedById == userId || OwnerId == userId;

    public TaskDto ToDto(HouseholdTask t, Guid userId) => new(
        t.Id,
        t.Title,
        t.Category,
        t.AssigneeId,
        NameOf(t.AssigneeId),
        t.CreatedById,
        NameOf(t.CreatedById),
        t.DueOn,
        t.Recurrence,
        t.CompletedAt,
        t.CompletedById is { } by ? NameOf(by) : null,
        t.PaidAmount,
        !t.IsDone && t.CanBeCompletedBy(userId),
        CanDelete(t, userId));
}
