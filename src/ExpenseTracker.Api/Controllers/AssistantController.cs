using ExpenseTracker.Api.Contracts;
using ExpenseTracker.Application.Features.Assistant.Commands.AskAssistant;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[ApiController]
[Route("api/assistant")]
public sealed class AssistantController(ISender mediator) : ControllerBase
{
    [HttpPost("chat")]
    public async Task<ActionResult<AssistantReply>> Chat(AssistantChatRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new AskAssistantCommand(request.Messages ?? [], request.Today), ct));
}
