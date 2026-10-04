using ExpenseTracker.Application.Common.Exceptions;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Features.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Application.Features.Households.Commands.LeaveHousehold;

/// <summary>
/// The user goes back to their personal ledger; the household's expenses stay with the household and their open
/// household tasks pass to the owner.
/// </summary>
public sealed record LeaveHouseholdCommand : IRequest;

public sealed class LeaveHouseholdCommandHandler(
    IAppDbContext db, IUserContext user, IUserDirectory users, ILogger<LeaveHouseholdCommandHandler> logger)
    : IRequestHandler<LeaveHouseholdCommand>
{
    public async Task Handle(LeaveHouseholdCommand request, CancellationToken cancellationToken)
    {
        var household = await db.FindForUserAsync(user.UserId, cancellationToken)
            ?? throw new NotFoundException("Bir hanede değilsin.");

        household.Leave(user.UserId);
        await db.HandOverOpenTasksAsync(household, user.UserId, users, logger, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {UserId} left household {HouseholdId}", user.UserId, household.Id);
    }
}
