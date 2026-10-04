using ExpenseTracker.Application.Common.Ai;
using ExpenseTracker.Application.Common.Exceptions;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Common.Logging;
using ExpenseTracker.Application.Features.Insights.Reviews;
using ExpenseTracker.Domain.Entities;
using ExpenseTracker.Domain.Enums;
using ExpenseTracker.Domain.Insights;
using ExpenseTracker.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Application.Features.Insights.Plans.Commands.CreateBudgetPlan;

/// <summary>"Save <paramref name="GoalAmount"/> in <paramref name="Months"/> months": the planner proposes how. Nothing is applied yet.</summary>
public sealed record CreateBudgetPlanCommand(decimal GoalAmount, int Months, string? Purpose) : IRequest<BudgetPlanDto>;

public sealed class CreateBudgetPlanCommandValidator : AbstractValidator<CreateBudgetPlanCommand>
{
    public CreateBudgetPlanCommandValidator()
    {
        RuleFor(x => x.GoalAmount).GreaterThan(0).LessThanOrEqualTo(Budget.MaxAmount).PrecisionScale(10, 2, ignoreTrailingZeros: true);
        RuleFor(x => x.Months).InclusiveBetween(1, BudgetPlan.MaxMonths);
        RuleFor(x => x.Purpose).MaximumLength(BudgetPlan.PurposeMaxLength);
    }
}

public sealed class CreateBudgetPlanCommandHandler(
    IAppDbContext db,
    IUserContext user,
    IUserDirectory users,
    IAppClock clock,
    IChatClient chatClient,
    ILogger<CreateBudgetPlanCommandHandler> logger)
    : IRequestHandler<CreateBudgetPlanCommand, BudgetPlanDto>
{
    /// <summary>The plan is based on the last complete months; three smooth out one unusual month.</summary>
    private const int BaselineMonths = 3;

    public async Task<BudgetPlanDto> Handle(CreateBudgetPlanCommand request, CancellationToken cancellationToken)
    {
        var ledgerId = user.LedgerId;
        var thisMonth = BudgetMonth.From(clock.Today);
        var from = thisMonth.FirstDay.AddMonths(-BaselineMonths);

        var recent = await db.Expenses.AsNoTracking()
            .Where(e => e.LedgerId == ledgerId && e.SpentOn >= from && e.SpentOn < thisMonth.FirstDay)
            .Select(e => new SpendingRow(e.Id, e.Amount, e.Category, e.SpentOn, e.Note, e.UserId))
            .ToListAsync(cancellationToken);

        // Average over the months that have any spending, so a ledger started last month is not diluted by empty ones.
        var monthsWithData = recent.Select(e => (e.SpentOn.Year, e.SpentOn.Month)).Distinct().Count();
        if (monthsWithData == 0)
            throw new ConflictException("Plan için en az bir tam ayın harcaması gerekiyor. Bu ay bitince tekrar dene.");

        IReadOnlyDictionary<ExpenseCategory, decimal> baseline = recent
            .GroupBy(e => e.Category)
            .ToDictionary(g => g.Key, g => decimal.Round(g.Sum(e => e.Amount) / monthsWithData, 2));

        var draft = await DraftAsync(request, baseline, recent, cancellationToken);
        var figures = PlanMath.Compute(request.GoalAmount, request.Months, BudgetPlan.MaxMonths, baseline, draft.Choices)
            ?? throw new ConflictException(
                $"Bu hedef mevcut harcamalarla {BudgetPlan.MaxMonths} ayda bile karşılanamıyor. Daha küçük bir tutar dene.");
        if (figures.MonthlyBudget <= 0)
            throw new ConflictException("Bu hedef için harcamaların neredeyse tamamını kısmak gerekiyor. Daha küçük bir tutar ya da daha uzun süre dene.");

        var plan = BudgetPlan.Create(
            ledgerId, user.UserId, request.Purpose, request.GoalAmount, request.Months, figures.Months,
            BudgetMonth.From(thisMonth.FirstDay.AddMonths(1)), decimal.Round(baseline.Values.Sum(), 2), figures.MonthlyBudget,
            figures.Cuts, draft.Advice, draft.GeneratedBy);
        db.BudgetPlans.Add(plan);
        await db.SaveChangesAsync(cancellationToken);

        logger.BudgetPlanCreated(plan.Id, ledgerId, plan.Months, plan.GeneratedBy);
        return await BudgetPlanDto.FromAsync(plan, users, cancellationToken);
    }

    private async Task<PlanDraft> DraftAsync(
        CreateBudgetPlanCommand request, IReadOnlyDictionary<ExpenseCategory, decimal> baseline, List<SpendingRow> recent,
        CancellationToken ct)
    {
        if (!AiModel.IsConfigured(chatClient))
            return PlannerAgent.Rules(baseline, request.Purpose);

        try
        {
            var model = AiModel.Name(chatClient);
            return await new PlannerAgent(chatClient, model)
                .RunAsync(request.GoalAmount, request.Months, request.Purpose, baseline, recent, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The arithmetic does not need a model; without one the rules choose the categories.
            logger.InsightModelUnavailable("budget-plan", ex.GetType().Name);
            return PlannerAgent.Rules(baseline, request.Purpose);
        }
    }
}
