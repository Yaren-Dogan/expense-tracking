using ExpenseTracker.Application.Common.Exceptions;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Common.Logging;
using ExpenseTracker.Application.Common.Text;
using ExpenseTracker.Domain.Entities;
using ExpenseTracker.Domain.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Application.Features.Insights.Plans.Commands.ApplyBudgetPlan;

/// <summary>A member accepts the plan: the budget of each planned month is set (created or changed). Happens once.</summary>
/// <returns>Null when the plan does not exist in the current ledger.</returns>
public sealed record ApplyBudgetPlanCommand(Guid Id) : IRequest<BudgetPlanDto?>;

public sealed class ApplyBudgetPlanCommandHandler(
    IAppDbContext db,
    IUserContext user,
    IUserDirectory users,
    TimeProvider time,
    ILogger<ApplyBudgetPlanCommandHandler> logger)
    : IRequestHandler<ApplyBudgetPlanCommand, BudgetPlanDto?>
{
    public async Task<BudgetPlanDto?> Handle(ApplyBudgetPlanCommand request, CancellationToken cancellationToken)
    {
        var ledgerId = user.LedgerId;
        var plan = await db.BudgetPlans.SingleOrDefaultAsync(p => p.Id == request.Id && p.LedgerId == ledgerId, cancellationToken);
        if (plan is null)
            return null;
        if (plan.IsApplied)
            throw new ConflictException("Bu plan zaten uygulandı.");

        var months = plan.PlannedMonths().ToList();
        var years = months.Select(m => m.Year).Distinct().ToList();
        var existing = await db.Budgets
            .Where(b => b.LedgerId == ledgerId && years.Contains(b.Year))
            .ToListAsync(cancellationToken);
        foreach (var month in months)
        {
            var budget = existing.SingleOrDefault(b => b.Year == month.Year && b.Month == month.Month);
            if (budget is null)
                db.Budgets.Add(Budget.Create(ledgerId, month, plan.MonthlyBudget));
            else
                budget.ChangeAmount(plan.MonthlyBudget);
        }

        plan.MarkApplied(user.UserId, time.GetUtcNow());

        var names = await users.GetDisplayNamesAsync([user.UserId], cancellationToken);
        var message = $"{names.GetValueOrDefault(user.UserId) ?? "Bir üye"} birikim planını uyguladı: " +
                      $"{TurkishText.Month(months[0])} – {TurkishText.Month(months[^1])} aylık bütçe {TurkishText.Money(plan.MonthlyBudget)}" +
                      (string.IsNullOrWhiteSpace(plan.Purpose) ? "." : $" ({plan.Purpose}).");
        foreach (var member in (await Insights.LedgerMembers.ResolveAsync(db, users, ledgerId, cancellationToken)).Where(m => m != user.UserId))
            db.Notifications.Add(Notification.Create(member, NotificationKind.BudgetPlanApplied, plan.Id, message));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            // Someone set one of these months' budget at the same moment; nothing was written, so the user can retry.
            throw new ConflictException("Bu aylardan birinin bütçesi az önce değişti. Planı tekrar uygula.");
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another member applied this plan between our read and our write (applied_at is a concurrency token);
            // the whole save was rolled back, so their apply is the only one.
            throw new ConflictException("Bu plan zaten uygulandı.");
        }

        logger.BudgetPlanApplied(plan.Id, ledgerId, months.Count);
        return await BudgetPlanDto.FromAsync(plan, users, cancellationToken);
    }
}
