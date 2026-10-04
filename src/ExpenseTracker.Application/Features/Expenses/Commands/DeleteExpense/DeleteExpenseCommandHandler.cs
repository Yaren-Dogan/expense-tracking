using ExpenseTracker.Application.Common.Exceptions;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Common.Logging;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Application.Features.Expenses.Commands.DeleteExpense;

public sealed class DeleteExpenseCommandHandler(
    IAppDbContext db,
    IUserContext user,
    ILogger<DeleteExpenseCommandHandler> logger)
    : IRequestHandler<DeleteExpenseCommand, bool>
{
    public async Task<bool> Handle(DeleteExpenseCommand request, CancellationToken cancellationToken)
    {
        var (userId, ledgerId) = (user.UserId, user.LedgerId);
        var expense = await db.Expenses
            .SingleOrDefaultAsync(e => e.Id == request.Id && e.LedgerId == ledgerId, cancellationToken);

        if (expense is null)
            return false;

        // Household members see each other's expenses but may only delete their own.
        if (!expense.WasCreatedBy(userId))
            throw new ForbiddenException("Sadece kendi girdiğin harcamaları silebilirsin.");

        db.Expenses.Remove(expense);
        await db.SaveChangesAsync(cancellationToken);

        logger.ExpenseDeleted(expense.Id, userId);
        return true;
    }
}
