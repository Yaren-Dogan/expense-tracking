using ExpenseTracker.Api.Contracts;
using ExpenseTracker.Application.Features.Assistant.Commands.AskAssistant;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ExpenseTracker.Api.Controllers;

[ApiController]
[Route("api/assistant")]
public sealed class AssistantController(ISender mediator) : ControllerBase
{
    [EnableRateLimiting(RateLimitPolicies.Ai)]
    [HttpPost("chat")]
    public async Task<ActionResult<AssistantReply>> Chat(AssistantChatRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new AskAssistantCommand(request.Messages ?? [], request.Today), ct));
}
