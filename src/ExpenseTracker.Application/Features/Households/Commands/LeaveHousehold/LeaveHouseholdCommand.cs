using ExpenseTracker.Application.Common.Exceptions;
using ExpenseTracker.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Application.Features.Households.Commands.LeaveHousehold;

/// <summary>The user goes back to their personal ledger; the household's expenses stay with the household.</summary>
public sealed record LeaveHouseholdCommand : IRequest;

public sealed class LeaveHouseholdCommandHandler(IAppDbContext db, IUserContext user, ILogger<LeaveHouseholdCommandHandler> logger)
    : IRequestHandler<LeaveHouseholdCommand>
{
    public async Task Handle(LeaveHouseholdCommand request, CancellationToken cancellationToken)
    {
        var household = await db.FindForUserAsync(user.UserId, cancellationToken)
            ?? throw new NotFoundException("Bir hanede değilsin.");

        household.Leave(user.UserId);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {UserId} left household {HouseholdId}", user.UserId, household.Id);
    }
}
