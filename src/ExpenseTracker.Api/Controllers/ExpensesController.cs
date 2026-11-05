using ExpenseTracker.Api.Contracts;
using ExpenseTracker.Application.Features.Expenses;
using ExpenseTracker.Application.Features.Expenses.Commands.CreateExpense;
using ExpenseTracker.Application.Features.Expenses.Commands.DeleteExpense;
using ExpenseTracker.Application.Features.Expenses.Queries.ExportExpenses;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Features.Expenses.Queries.ListExpenses;
using ExpenseTracker.Application.Features.Expenses.Queries.ParseExpense;
using ExpenseTracker.Application.Features.Expenses.Queries.SearchExpenses;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ExpenseTracker.Api.Controllers;

[ApiController]
[Route("api/expenses")]
public sealed class ExpensesController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExpenseDto>>> List([FromQuery] string? month, CancellationToken ct) =>
        Ok(await mediator.Send(new ListExpensesQuery(month), ct));

    /// <summary>The month's expenses as a CSV download (Excel, Google Sheets).</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string month, CancellationToken ct)
    {
        var file = await mediator.Send(new ExportExpensesQuery(month), ct);
        return File(file.Content, "text/csv; charset=utf-8", file.FileName);
    }

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

    /// <summary>Suggests an expense from a sentence. Does not save anything.</summary>
    [EnableRateLimiting(RateLimitPolicies.Ai)]
    [HttpPost("parse")]
    public async Task<ActionResult<ParsedExpenseDto>> Parse(ParseExpenseRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new ParseExpenseQuery(request.Text, request.Today), ct));

    /// <summary>Embeds the query with the provider on every call, so it shares the model quota and its limit.</summary>
    [EnableRateLimiting(RateLimitPolicies.Ai)]
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<ExpenseMatch>>> Search(
        [FromQuery] string q, [FromQuery] int top = 10, CancellationToken ct = default) =>
        Ok(await mediator.Send(new SearchExpensesQuery(q, top), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await mediator.Send(new DeleteExpenseCommand(id), ct) ? NoContent() : NotFound();
}
