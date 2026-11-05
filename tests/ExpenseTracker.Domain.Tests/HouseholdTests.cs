using ExpenseTracker.Domain.Common;
using ExpenseTracker.Domain.Households;
using FluentAssertions;

namespace ExpenseTracker.Domain.Tests;

public class HouseholdTests
{
    private static readonly Guid Ayse = Guid.NewGuid(), Mehmet = Guid.NewGuid(), Can = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private static Household Family() => Household.Create("Yılmaz Ailesi", Ayse, InviteCode.New(), T0);

    [Fact]
    public void The_creator_is_the_owner()
    {
        var h = Family();

        h.IsOwner(Ayse).Should().BeTrue();
        h.Members.Should().ContainSingle();
    }

    [Fact]
    public void Joining_twice_is_rejected()
    {
        var h = Family();
        h.Join(Mehmet, T0.AddDays(1));

        var act = () => h.Join(Mehmet, T0.AddDays(2));
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Ownership_passes_to_the_longest_standing_member_when_the_owner_leaves()
    {
        var h = Family();
        h.Join(Can, T0.AddDays(2));
        h.Join(Mehmet, T0.AddDays(1));

        h.Leave(Ayse);

        h.IsOwner(Mehmet).Should().BeTrue();
        h.IsOwner(Can).Should().BeFalse();
    }

    [Fact]
    public void Only_the_owner_removes_members_or_changes_the_code()
    {
        var h = Family();
        h.Join(Mehmet, T0.AddDays(1));
        h.Join(Can, T0.AddDays(2));

        ((Action)(() => h.RemoveMember(Mehmet, Can, "NEWCODE2"))).Should().Throw<DomainException>();
        ((Action)(() => h.ReplaceInviteCode(Mehmet, InviteCode.New()))).Should().Throw<DomainException>();

        h.RemoveMember(Ayse, Can, "NEWCODE2");
        h.IsMember(Can).Should().BeFalse();
    }

    [Fact]
    public void Membership_is_capped()
    {
        var h = Family();
        for (var i = 1; i < Household.MaxMembers; i++)
            h.Join(Guid.NewGuid(), T0.AddMinutes(i));

        var act = () => h.Join(Guid.NewGuid(), T0.AddDays(1));
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("abcd-2345", true)]
    [InlineData("ABCD2345", true)]
    [InlineData("ABCD234", false)]
    [InlineData("ABCD0345", false)]
    public void Invite_codes_are_normalised_and_avoid_ambiguous_characters(string code, bool valid)
    {
        InviteCode.IsWellFormed(code).Should().Be(valid);
        InviteCode.IsWellFormed(InviteCode.New()).Should().BeTrue();
    }
}
