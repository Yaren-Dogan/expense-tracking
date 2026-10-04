using ExpenseTracker.Application.Common.Exceptions;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain.Households;
using MediatR;

namespace ExpenseTracker.Application.Features.Households.Commands.RegenerateInviteCode;

/// <summary>Invalidates the old code, e.g. after it was shared somewhere it should not have been.</summary>
public sealed record RegenerateInviteCodeCommand : IRequest<HouseholdDto>;

public sealed class RegenerateInviteCodeCommandHandler(IAppDbContext db, IUserContext user, IUserDirectory users)
    : IRequestHandler<RegenerateInviteCodeCommand, HouseholdDto>
{
    public async Task<HouseholdDto> Handle(RegenerateInviteCodeCommand request, CancellationToken cancellationToken)
    {
        var household = await db.FindForUserAsync(user.UserId, cancellationToken)
            ?? throw new NotFoundException("Bir hanede değilsin.");
        if (!household.IsOwner(user.UserId))
            throw new ForbiddenException("Davet kodunu sadece haneyi kuran kişi yenileyebilir.");

        household.ReplaceInviteCode(user.UserId, InviteCode.New());
        await db.SaveWithUniqueRetryAsync(household, user.UserId, cancellationToken);
        return await household.ToDtoAsync(user.UserId, users, cancellationToken);
    }
}
