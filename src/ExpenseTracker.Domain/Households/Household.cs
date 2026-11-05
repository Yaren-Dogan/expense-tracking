using ExpenseTracker.Domain.Common;

namespace ExpenseTracker.Domain.Households;

/// <summary>
/// A shared ledger for a couple or family. Its Id is the ledger id of every shared expense and budget,
/// so joining or leaving never copies data: it only changes which ledger a user writes to and reads from.
/// </summary>
public sealed class Household : Entity
{
    public const int NameMaxLength = 60;
    public const int MaxMembers = 10;

    private readonly List<HouseholdMember> _members = [];

    public string Name { get; private set; } = string.Empty;
    public string InviteCode { get; private set; } = string.Empty;
    public IReadOnlyList<HouseholdMember> Members => _members;

    private Household() { }

    public static Household Create(string name, Guid ownerUserId, string inviteCode, DateTimeOffset now)
    {
        var household = new Household { Name = ValidName(name), InviteCode = inviteCode };
        household._members.Add(new HouseholdMember(household.Id, ownerUserId, HouseholdRole.Owner, now));
        return household;
    }

    public bool IsMember(Guid userId) => _members.Any(m => m.UserId == userId);

    public bool IsOwner(Guid userId) => _members.Any(m => m.UserId == userId && m.Role == HouseholdRole.Owner);

    public void Join(Guid userId, DateTimeOffset now)
    {
        if (!IsOpen)
            throw new DomainException("Nobody is in this household any more.");
        if (IsMember(userId))
            throw new DomainException("Already a member of this household.");
        if (_members.Count >= MaxMembers)
            throw new DomainException($"A household can have at most {MaxMembers} members.");

        _members.Add(new HouseholdMember(Id, userId, HouseholdRole.Member, now));
    }

    public void Leave(Guid userId)
    {
        var member = _members.SingleOrDefault(m => m.UserId == userId)
            ?? throw new DomainException("Not a member of this household.");

        _members.Remove(member);

        // The household must keep an owner who can manage invites: hand it to the longest-standing member.
        if (member.Role == HouseholdRole.Owner && _members.Count > 0)
            _members.OrderBy(m => m.JoinedAt).First().Role = HouseholdRole.Owner;
    }

    /// <summary>
    /// Removes a member and retires the invite code: every member has seen it, so keeping it would let the removed person
    /// join straight back and read everything the household writes afterwards.
    /// </summary>
    public void RemoveMember(Guid ownerUserId, Guid memberUserId, string newInviteCode)
    {
        if (!IsOwner(ownerUserId))
            throw new DomainException("Only the owner can remove members.");
        if (ownerUserId == memberUserId)
            throw new DomainException("The owner leaves instead of removing themselves.");

        Leave(memberUserId);
        ReplaceInviteCode(ownerUserId, newInviteCode);
    }

    /// <summary>
    /// A household nobody is in any more cannot be joined: there is no owner to manage the code, and an old code (seen by
    /// former members, or pasted in a group chat) would otherwise open the family's whole history to whoever finds it.
    /// </summary>
    public bool IsOpen => _members.Count > 0;

    public void ReplaceInviteCode(Guid ownerUserId, string inviteCode)
    {
        if (!IsOwner(ownerUserId))
            throw new DomainException("Only the owner can change the invite code.");
        InviteCode = inviteCode;
    }

    private static string ValidName(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > NameMaxLength)
            throw new DomainException($"Household name must be 1-{NameMaxLength} characters.");
        return trimmed;
    }
}
