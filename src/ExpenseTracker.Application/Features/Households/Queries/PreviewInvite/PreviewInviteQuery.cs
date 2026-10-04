using ExpenseTracker.Application.Common.Exceptions;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain.Households;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Features.Households.Queries.PreviewInvite;

public sealed record PreviewInviteQuery(string Code) : IRequest<InvitePreviewDto>;

public sealed class PreviewInviteQueryValidator : AbstractValidator<PreviewInviteQuery>
{
    public PreviewInviteQueryValidator() => RuleFor(x => x.Code).ValidInviteCode();
}

public sealed class PreviewInviteQueryHandler(IAppDbContext db, IUserDirectory users)
    : IRequestHandler<PreviewInviteQuery, InvitePreviewDto>
{
    public async Task<InvitePreviewDto> Handle(PreviewInviteQuery request, CancellationToken cancellationToken)
    {
        var code = InviteCode.Normalize(request.Code);
        var household = await db.Households.AsNoTracking().Include(h => h.Members)
            .SingleOrDefaultAsync(h => h.InviteCode == code, cancellationToken)
            ?? throw new NotFoundException("Bu davet kodu geçerli değil.");

        var names = await users.GetDisplayNamesAsync(household.Members.Select(m => m.UserId), cancellationToken);
        var memberNames = household.Members.OrderBy(m => m.JoinedAt).Select(m => names.GetValueOrDefault(m.UserId) ?? "?").ToList();
        return new InvitePreviewDto(household.Name, memberNames);
    }
}
