using ExpenseTracker.Application.Common.Exceptions;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain.Households;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Application.Features.Households.Commands.JoinHousehold;

public sealed record JoinHouseholdCommand(string Code) : IRequest<HouseholdDto>;

public sealed class JoinHouseholdCommandValidator : AbstractValidator<JoinHouseholdCommand>
{
    public JoinHouseholdCommandValidator() => RuleFor(x => x.Code).ValidInviteCode();
}

public sealed class JoinHouseholdCommandHandler(
    IAppDbContext db, IUserContext user, IUserDirectory users, TimeProvider time, ILogger<JoinHouseholdCommandHandler> logger)
    : IRequestHandler<JoinHouseholdCommand, HouseholdDto>
{
    public async Task<HouseholdDto> Handle(JoinHouseholdCommand request, CancellationToken cancellationToken)
    {
        if (await db.FindForUserAsync(user.UserId, cancellationToken) is not null)
            throw new ConflictException("Zaten bir hanedesin. Başka bir haneye katılmak için önce mevcut haneden ayrıl.");

        var code = InviteCode.Normalize(request.Code);
        var household = await db.Households.Include(h => h.Members)
            .SingleOrDefaultAsync(h => h.InviteCode == code, cancellationToken)
            ?? throw new NotFoundException("Bu davet kodu geçerli değil.");

        if (household.Members.Count >= Household.MaxMembers)
            throw new ConflictException($"Bu hane dolu (en fazla {Household.MaxMembers} kişi).");

        household.Join(user.UserId, time.GetUtcNow());
        await db.SaveWithUniqueRetryAsync(household, user.UserId, cancellationToken);

        logger.LogInformation("User {UserId} joined household {HouseholdId}", user.UserId, household.Id);
        return await household.ToDtoAsync(user.UserId, users, cancellationToken);
    }
}
