using ExpenseTracker.Application.Common.Exceptions;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Common.Logging;
using ExpenseTracker.Domain.Entities;
using ExpenseTracker.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Application.Features.Budgets.Commands.SetBudget;

public sealed class SetBudgetCommandHandler(
    IAppDbContext db,
    IUserContext user,
    ILogger<SetBudgetCommandHandler> logger)
    : IRequestHandler<SetBudgetCommand, BudgetDto>
{
    public async Task<BudgetDto> Handle(SetBudgetCommand request, CancellationToken cancellationToken)
    {
        BudgetMonth.TryParse(request.Month, out var month);
        var userId = user.UserId;

        var budget = await Find(userId, month, cancellationToken);
        if (budget is not null)
        {
            budget.ChangeAmount(request.Amount);
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            budget = Budget.Create(userId, month, request.Amount);
            db.Budgets.Add(budget);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (UniqueConstraintViolationException)
            {
                // Two first-time saves for the same month raced; update the row the other request inserted.
                db.Entry(budget).State = EntityState.Detached;
                budget = await Find(userId, month, cancellationToken)
                    ?? throw new InvalidOperationException("Unique violation on budget month but no matching budget was found.");
                budget.ChangeAmount(request.Amount);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        logger.BudgetSet(month.ToString(), budget.Amount, userId);
        return BudgetDto.From(budget);
    }

    private Task<Budget?> Find(Guid userId, BudgetMonth month, CancellationToken ct) =>
        db.Budgets.SingleOrDefaultAsync(b => b.UserId == userId && b.Year == month.Year && b.Month == month.Month, ct);
}
