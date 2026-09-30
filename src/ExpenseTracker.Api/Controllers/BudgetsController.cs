using ExpenseTracker.Api.Contracts;
using ExpenseTracker.Application.Features.Budgets;
using ExpenseTracker.Application.Features.Budgets.Commands.SetBudget;
using ExpenseTracker.Application.Features.Budgets.Queries.GetBudget;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[ApiController]
[Route("api/budgets/{month}")]
public sealed class BudgetsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BudgetDto>> Get(string month, CancellationToken ct) =>
        await mediator.Send(new GetBudgetQuery(month), ct) is { } budget ? Ok(budget) : NotFound();

    [HttpPut]
    public async Task<ActionResult<BudgetDto>> Set(string month, SetBudgetRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SetBudgetCommand(month, request.Amount), ct));
}
