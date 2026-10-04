using ExpenseTracker.Api.Contracts;
using ExpenseTracker.Application.Features.Tasks;
using ExpenseTracker.Application.Features.Tasks.Commands.CompleteTask;
using ExpenseTracker.Application.Features.Tasks.Commands.CreateTask;
using ExpenseTracker.Application.Features.Tasks.Commands.DeleteTask;
using ExpenseTracker.Application.Features.Tasks.Queries.ListTasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[ApiController]
[Route("api/tasks")]
public sealed class TasksController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<TaskListDto>> List(CancellationToken ct) =>
        Ok(await mediator.Send(new ListTasksQuery(), ct));

    [HttpPost]
    public async Task<ActionResult<TaskDto>> Create(CreateTaskRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await mediator.Send(
            new CreateTaskCommand(request.Title, request.AssigneeId, request.DueOn, request.Recurrence, request.Category), ct));

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<CompleteTaskResult>> Complete(Guid id, CompleteTaskRequest? request, CancellationToken ct) =>
        await mediator.Send(new CompleteTaskCommand(id, request?.Amount), ct) is { } result ? Ok(result) : NotFound();

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await mediator.Send(new DeleteTaskCommand(id), ct) ? NoContent() : NotFound();
}
