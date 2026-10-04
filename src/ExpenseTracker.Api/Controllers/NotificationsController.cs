using ExpenseTracker.Application.Features.Notifications;
using ExpenseTracker.Application.Features.Notifications.Commands.MarkNotificationsRead;
using ExpenseTracker.Application.Features.Notifications.Queries.ListNotifications;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<NotificationListDto>> List(CancellationToken ct) =>
        Ok(await mediator.Send(new ListNotificationsQuery(), ct));

    [HttpPost("read")]
    public async Task<IActionResult> MarkRead(CancellationToken ct)
    {
        await mediator.Send(new MarkNotificationsReadCommand(), ct);
        return NoContent();
    }
}
