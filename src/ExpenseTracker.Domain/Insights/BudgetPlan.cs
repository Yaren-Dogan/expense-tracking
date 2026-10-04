using ExpenseTracker.Domain.Common;
using ExpenseTracker.Domain.Enums;
using ExpenseTracker.Domain.ValueObjects;

namespace ExpenseTracker.Domain.Insights;

/// <summary>
/// A savings plan proposed by the planner agent: "save ₺20.000 in 4 months" becomes a monthly budget for each of those
/// months and a target for the categories to trim. Nothing changes until a member applies it (human in the loop);
/// applying writes the budgets and can happen once.
/// </summary>
public sealed class BudgetPlan : Entity
{
    public const int PurposeMaxLength = 100;
    public const int AdviceMaxLength = 400;
    public const int MaxMonths = 24;

    public Guid LedgerId { get; private set; }
    public Guid CreatedById { get; private set; }
    public string Purpose { get; private set; } = string.Empty;
    public decimal GoalAmount { get; private set; }
    public int RequestedMonths { get; private set; }

    /// <summary>The months the plan really takes: the requested ones, or more when the goal did not fit.</summary>
    public int Months { get; private set; }
    public int StartYear { get; private set; }
    public int StartMonth { get; private set; }

    /// <summary>Average monthly spending of the recent months the plan is based on.</summary>
    public decimal BaselineMonthly { get; private set; }
    public decimal MonthlyBudget { get; private set; }
    public IReadOnlyList<PlanCut> Cuts { get; private set; } = [];
    public string Advice { get; private set; } = string.Empty;
    public string GeneratedBy { get; private set; } = MonthReview.RulesAuthor;
    public DateTimeOffset? AppliedAt { get; private set; }
    public Guid? AppliedById { get; private set; }

    public BudgetMonth Start => new(StartYear, StartMonth);
    public bool IsExtended => Months > RequestedMonths;
    public bool IsApplied => AppliedAt is not null;

    private BudgetPlan() { }

    public static BudgetPlan Create(
        Guid ledgerId, Guid createdById, string? purpose, decimal goalAmount, int requestedMonths, int months,
        BudgetMonth start, decimal baselineMonthly, decimal monthlyBudget, IEnumerable<PlanCut> cuts, string advice, string generatedBy)
    {
        if (ledgerId == Guid.Empty || createdById == Guid.Empty)
            throw new DomainException("A plan needs a ledger and a creator.");
        if (goalAmount <= 0)
            throw new DomainException("The goal must be greater than zero.");
        if (requestedMonths is < 1 or > MaxMonths || months < requestedMonths || months > MaxMonths)
            throw new DomainException($"A plan lasts 1-{MaxMonths} months.");
        if (monthlyBudget <= 0 || baselineMonthly <= 0)
            throw new DomainException("A plan needs a positive baseline and budget.");

        var plan = new BudgetPlan
        {
            LedgerId = ledgerId,
            CreatedById = createdById,
            Purpose = Clip(purpose?.Trim() ?? string.Empty, PurposeMaxLength),
            GoalAmount = goalAmount,
            RequestedMonths = requestedMonths,
            Months = months,
            StartYear = start.Year,
            StartMonth = start.Month,
            BaselineMonthly = baselineMonthly,
            MonthlyBudget = monthlyBudget,
            Advice = Clip(advice?.Trim() ?? string.Empty, AdviceMaxLength),
            GeneratedBy = string.IsNullOrWhiteSpace(generatedBy) ? MonthReview.RulesAuthor : generatedBy.Trim(),
            Cuts = cuts.ToList()
        };
        return plan;
    }

    /// <summary>The months whose budget applying the plan sets.</summary>
    public IEnumerable<BudgetMonth> PlannedMonths()
    {
        var first = Start.FirstDay;
        return Enumerable.Range(0, Months).Select(i => BudgetMonth.From(first.AddMonths(i)));
    }

    public void MarkApplied(Guid userId, DateTimeOffset now)
    {
        if (IsApplied)
            throw new DomainException("The plan has already been applied.");
        AppliedAt = now;
        AppliedById = userId;
    }

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}

/// <summary>One category the plan trims: from its recent monthly average to a target.</summary>
public sealed record PlanCut(ExpenseCategory Category, CutLevel Level, decimal CurrentMonthly, decimal TargetMonthly, string Reason);
