using ExpenseTracker.Domain.Households;

namespace ExpenseTracker.Application.Features.Households;

/// <param name="InviteCode">Shown to every member so anyone can invite family; only the owner can replace it.</param>
public sealed record HouseholdDto(
    Guid Id,
    string Name,
    string InviteCode,
    bool IsOwner,
    IReadOnlyList<HouseholdMemberDto> Members);

public sealed record HouseholdMemberDto(Guid UserId, string DisplayName, HouseholdRole Role, DateTimeOffset JoinedAt, bool IsMe);

/// <summary>What a person sees before joining ("Aynı haneden misiniz?"): enough to recognise the household, nothing more.</summary>
public sealed record InvitePreviewDto(string HouseholdName, IReadOnlyList<string> MemberNames);
