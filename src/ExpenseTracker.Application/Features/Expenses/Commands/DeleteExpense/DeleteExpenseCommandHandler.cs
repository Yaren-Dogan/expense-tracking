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
        var userId = user.UserId;
        var expense = await db.Expenses
            .SingleOrDefaultAsync(e => e.Id == request.Id && e.UserId == userId, cancellationToken);

        if (expense is null)
            return false;

        db.Expenses.Remove(expense);
        await db.SaveChangesAsync(cancellationToken);

        logger.ExpenseDeleted(expense.Id, userId);
        return true;
    }
}
