using ExpenseTracker.Api.Contracts;
using ExpenseTracker.Application.Features.Expenses;
using ExpenseTracker.Application.Features.Expenses.Commands.CreateExpense;
using ExpenseTracker.Application.Features.Expenses.Commands.DeleteExpense;
using ExpenseTracker.Application.Features.Expenses.Queries.ListExpenses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[ApiController]
[Route("api/expenses")]
public sealed class ExpensesController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExpenseDto>>> List([FromQuery] string? month, CancellationToken ct) =>
        Ok(await mediator.Send(new ListExpensesQuery(month), ct));

    [HttpPost]
    public async Task<ActionResult<ExpenseDto>> Create(
        CreateExpenseRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new CreateExpenseCommand(request.Amount, request.Category, request.Note, request.SpentOn, idempotencyKey), ct);

        return result.IsReplay
            ? Ok(result.Expense)
            : StatusCode(StatusCodes.Status201Created, result.Expense);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await mediator.Send(new DeleteExpenseCommand(id), ct) ? NoContent() : NotFound();
}
