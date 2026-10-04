using ExpenseTracker.Api.Contracts;
using ExpenseTracker.Application.Features.Households;
using ExpenseTracker.Application.Features.Households.Commands.CreateHousehold;
using ExpenseTracker.Application.Features.Households.Commands.JoinHousehold;
using ExpenseTracker.Application.Features.Households.Commands.LeaveHousehold;
using ExpenseTracker.Application.Features.Households.Commands.RegenerateInviteCode;
using ExpenseTracker.Application.Features.Households.Commands.RemoveMember;
using ExpenseTracker.Application.Features.Households.Queries.GetMyHousehold;
using ExpenseTracker.Application.Features.Households.Queries.PreviewInvite;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ExpenseTracker.Api.Controllers;

[ApiController]
[Route("api/households")]
public sealed class HouseholdsController(ISender mediator) : ControllerBase
{
    [HttpGet("current")]
    public async Task<ActionResult<HouseholdDto>> Current(CancellationToken ct) =>
        await mediator.Send(new GetMyHouseholdQuery(), ct) is { } household ? Ok(household) : NoContent();

    [HttpPost]
    public async Task<ActionResult<HouseholdDto>> Create(CreateHouseholdRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await mediator.Send(new CreateHouseholdCommand(request.Name), ct));

    /// <summary>"Aynı haneden misiniz?": shows who is in the household before the user commits to joining.</summary>
    [EnableRateLimiting(RateLimitPolicies.Invites)]
    [HttpGet("invites/{code}")]
    public async Task<ActionResult<InvitePreviewDto>> Preview(string code, CancellationToken ct) =>
        Ok(await mediator.Send(new PreviewInviteQuery(code), ct));

    [EnableRateLimiting(RateLimitPolicies.Invites)]
    [HttpPost("join")]
    public async Task<ActionResult<HouseholdDto>> Join(JoinHouseholdRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new JoinHouseholdCommand(request.Code), ct));

    [HttpPost("leave")]
    public async Task<IActionResult> Leave(CancellationToken ct)
    {
        await mediator.Send(new LeaveHouseholdCommand(), ct);
        return NoContent();
    }

    [HttpDelete("members/{userId:guid}")]
    public async Task<ActionResult<HouseholdDto>> RemoveMember(Guid userId, CancellationToken ct) =>
        Ok(await mediator.Send(new RemoveMemberCommand(userId), ct));

    [HttpPost("invite-code")]
    public async Task<ActionResult<HouseholdDto>> RegenerateInviteCode(CancellationToken ct) =>
        Ok(await mediator.Send(new RegenerateInviteCodeCommand(), ct));
}
