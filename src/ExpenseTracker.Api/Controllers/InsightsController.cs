using ExpenseTracker.Api.Contracts;
using ExpenseTracker.Application.Features.Insights.Plans;
using ExpenseTracker.Application.Features.Insights.Plans.Commands.ApplyBudgetPlan;
using ExpenseTracker.Application.Features.Insights.Plans.Commands.CreateBudgetPlan;
using ExpenseTracker.Application.Features.Insights.Plans.Queries.GetLatestBudgetPlan;
using ExpenseTracker.Application.Features.Insights.Reviews;
using ExpenseTracker.Application.Features.Insights.Reviews.Commands.CreateMonthReview;
using ExpenseTracker.Application.Features.Insights.Reviews.Queries.GetMonthReview;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ExpenseTracker.Api.Controllers;

/// <summary>The two business agents: the month review and the budget planner.</summary>
[ApiController]
[Route("api")]
public sealed class InsightsController(ISender mediator) : ControllerBase
{
    /// <summary>No review yet is a normal answer (the screen asks on every load), so it is 204 rather than an error.</summary>
    [HttpGet("reviews/{month}")]
    public async Task<ActionResult<MonthReviewDto>> GetReview(string month, CancellationToken ct) =>
        await mediator.Send(new GetMonthReviewQuery(month), ct) is { } review ? Ok(review) : NoContent();

    /// <summary>Returns the existing review, or writes one; <c>refresh=true</c> rewrites it.</summary>
    [EnableRateLimiting(RateLimitPolicies.Ai)]
    [HttpPost("reviews/{month}")]
    public async Task<ActionResult<MonthReviewDto>> CreateReview(string month, [FromQuery] bool refresh, CancellationToken ct) =>
        Ok(await mediator.Send(new CreateMonthReviewCommand(month, refresh), ct));

    [HttpGet("plans/latest")]
    public async Task<ActionResult<BudgetPlanDto>> LatestPlan(CancellationToken ct) =>
        await mediator.Send(new GetLatestBudgetPlanQuery(), ct) is { } plan ? Ok(plan) : NoContent();

    [EnableRateLimiting(RateLimitPolicies.Ai)]
    [HttpPost("plans")]
    public async Task<ActionResult<BudgetPlanDto>> CreatePlan(CreateBudgetPlanRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created,
            await mediator.Send(new CreateBudgetPlanCommand(request.GoalAmount, request.Months, request.Purpose), ct));

    [HttpPost("plans/{id:guid}/apply")]
    public async Task<ActionResult<BudgetPlanDto>> ApplyPlan(Guid id, CancellationToken ct) =>
        await mediator.Send(new ApplyBudgetPlanCommand(id), ct) is { } plan ? Ok(plan) : NotFound();
}
