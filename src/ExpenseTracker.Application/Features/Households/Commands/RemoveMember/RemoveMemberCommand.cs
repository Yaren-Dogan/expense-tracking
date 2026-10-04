using ExpenseTracker.Application.Common.Exceptions;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Features.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Application.Features.Households.Commands.RemoveMember;

public sealed record RemoveMemberCommand(Guid MemberUserId) : IRequest<HouseholdDto>;

public sealed class RemoveMemberCommandHandler(
    IAppDbContext db, IUserContext user, IUserDirectory users, ILogger<RemoveMemberCommandHandler> logger)
    : IRequestHandler<RemoveMemberCommand, HouseholdDto>
{
    public async Task<HouseholdDto> Handle(RemoveMemberCommand request, CancellationToken cancellationToken)
    {
        var household = await db.FindForUserAsync(user.UserId, cancellationToken)
            ?? throw new NotFoundException("Bir hanede değilsin.");
        if (!household.IsOwner(user.UserId))
            throw new ForbiddenException("Üye çıkarmayı sadece haneyi kuran kişi yapabilir.");
        if (request.MemberUserId == user.UserId)
            throw new ConflictException("Kendini çıkaramazsın; haneden ayrılabilirsin.");
        if (!household.IsMember(request.MemberUserId))
            throw new NotFoundException("Bu kişi hanede değil.");

        household.RemoveMember(user.UserId, request.MemberUserId);
        await db.HandOverOpenTasksAsync(household, request.MemberUserId, users, logger, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {MemberId} removed from household {HouseholdId} by {OwnerId}", request.MemberUserId, household.Id, user.UserId);
        return await household.ToDtoAsync(user.UserId, users, cancellationToken);
    }
}
