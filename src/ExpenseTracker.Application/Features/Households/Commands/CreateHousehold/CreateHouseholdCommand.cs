using ExpenseTracker.Application.Common.Exceptions;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain.Households;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Application.Features.Households.Commands.CreateHousehold;

public sealed record CreateHouseholdCommand(string Name) : IRequest<HouseholdDto>;

public sealed class CreateHouseholdCommandValidator : AbstractValidator<CreateHouseholdCommand>
{
    public CreateHouseholdCommandValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(Household.NameMaxLength);
}

public sealed class CreateHouseholdCommandHandler(
    IAppDbContext db, IUserContext user, IUserDirectory users, TimeProvider time, ILogger<CreateHouseholdCommandHandler> logger)
    : IRequestHandler<CreateHouseholdCommand, HouseholdDto>
{
    public async Task<HouseholdDto> Handle(CreateHouseholdCommand request, CancellationToken cancellationToken)
    {
        if (await db.FindForUserAsync(user.UserId, cancellationToken) is not null)
            throw new ConflictException("Zaten bir hanedesin. Yeni hane kurmak için önce mevcut haneden ayrıl.");

        var household = Household.Create(request.Name, user.UserId, InviteCode.New(), time.GetUtcNow());
        db.Households.Add(household);
        await db.SaveWithUniqueRetryAsync(household, user.UserId, cancellationToken);

        logger.LogInformation("Household {HouseholdId} created by {UserId}", household.Id, user.UserId);
        return await household.ToDtoAsync(user.UserId, users, cancellationToken);
    }
}
