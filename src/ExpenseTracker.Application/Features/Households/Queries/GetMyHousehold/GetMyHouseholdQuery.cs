using ExpenseTracker.Application.Common.Interfaces;
using MediatR;

namespace ExpenseTracker.Application.Features.Households.Queries.GetMyHousehold;

/// <returns>Null when the user is not in a household.</returns>
public sealed record GetMyHouseholdQuery : IRequest<HouseholdDto?>;

public sealed class GetMyHouseholdQueryHandler(IAppDbContext db, IUserContext user, IUserDirectory users)
    : IRequestHandler<GetMyHouseholdQuery, HouseholdDto?>
{
    public async Task<HouseholdDto?> Handle(GetMyHouseholdQuery request, CancellationToken cancellationToken)
    {
        var household = await db.FindForUserAsync(user.UserId, cancellationToken);
        return household is null ? null : await household.ToDtoAsync(user.UserId, users, cancellationToken);
    }
}
