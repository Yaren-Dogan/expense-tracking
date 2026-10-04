namespace ExpenseTracker.Domain.Households;

public sealed class HouseholdMember
{
    public Guid HouseholdId { get; private set; }
    public Guid UserId { get; private set; }
    public HouseholdRole Role { get; internal set; }
    public DateTimeOffset JoinedAt { get; private set; }

    private HouseholdMember() { }

    internal HouseholdMember(Guid householdId, Guid userId, HouseholdRole role, DateTimeOffset joinedAt)
    {
        HouseholdId = householdId;
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
    }
}
