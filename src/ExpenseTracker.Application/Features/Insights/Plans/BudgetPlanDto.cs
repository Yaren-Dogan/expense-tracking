using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain.Enums;
using ExpenseTracker.Domain.Insights;

namespace ExpenseTracker.Application.Features.Insights.Plans;

public sealed record PlanCutDto(ExpenseCategory Category, CutLevel Level, decimal CurrentMonthly, decimal TargetMonthly, string Reason);

/// <param name="IsExtended">The goal did not fit the requested months; <paramref name="Months"/> is how long it really takes.</param>
public sealed record BudgetPlanDto(
    Guid Id,
    string Purpose,
    decimal GoalAmount,
    int RequestedMonths,
    int Months,
    bool IsExtended,
    string StartMonth,
    string EndMonth,
    decimal BaselineMonthly,
    decimal MonthlySaving,
    decimal MonthlyBudget,
    IReadOnlyList<PlanCutDto> Cuts,
    string Advice,
    string GeneratedBy,
    string CreatedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AppliedAt,
    string? AppliedByName)
{
    public static async Task<BudgetPlanDto> FromAsync(BudgetPlan p, IUserDirectory users, CancellationToken ct)
    {
        var ids = new[] { p.CreatedById }.Concat(p.AppliedById is { } a ? [a] : Array.Empty<Guid>()).Distinct();
        var names = await users.GetDisplayNamesAsync(ids, ct);
        var months = p.PlannedMonths().ToList();
        return new BudgetPlanDto(
            p.Id, p.Purpose, p.GoalAmount, p.RequestedMonths, p.Months, p.IsExtended,
            months[0].ToString(), months[^1].ToString(),
            p.BaselineMonthly, p.BaselineMonthly - p.MonthlyBudget, p.MonthlyBudget,
            p.Cuts.Select(c => new PlanCutDto(c.Category, c.Level, c.CurrentMonthly, c.TargetMonthly, c.Reason)).ToList(),
            p.Advice, p.GeneratedBy, names.GetValueOrDefault(p.CreatedById) ?? "Eski üye", p.CreatedAt, p.AppliedAt,
            p.AppliedById is { } by ? names.GetValueOrDefault(by) ?? "Eski üye" : null);
    }
}
