using ExpenseTracker.Application.Common.Exceptions;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Common.Logging;
using ExpenseTracker.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Application.Features.Expenses.Commands.CreateExpense;

public sealed class CreateExpenseCommandHandler(
    IAppDbContext db,
    IUserContext user,
    ILogger<CreateExpenseCommandHandler> logger)
    : IRequestHandler<CreateExpenseCommand, CreateExpenseResult>
{
    public async Task<CreateExpenseResult> Handle(CreateExpenseCommand request, CancellationToken cancellationToken)
    {
        var userId = user.UserId;
        var key = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey;

        if (key is not null)
        {
            var existing = await FindByIdempotencyKey(userId, key, cancellationToken);
            if (existing is not null)
                return Replay(existing);
        }

        var expense = Expense.Create(userId, request.Amount, request.Category, request.Note, request.SpentOn, key);
        db.Expenses.Add(expense);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException) when (key is not null)
        {
            // A concurrent request with the same key won the race (e.g. a double tap); return its expense.
            db.Entry(expense).State = EntityState.Detached;
            var winner = await FindByIdempotencyKey(userId, key, cancellationToken)
                ?? throw new InvalidOperationException("Unique violation on idempotency key but no matching expense was found.");
            return Replay(winner);
        }

        logger.ExpenseCreated(expense.Id, userId, expense.Amount, expense.SpentOn);
        return new CreateExpenseResult(ExpenseDto.From(expense), IsReplay: false);
    }

    private Task<Expense?> FindByIdempotencyKey(Guid userId, string key, CancellationToken ct) =>
        db.Expenses.AsNoTracking().SingleOrDefaultAsync(e => e.UserId == userId && e.IdempotencyKey == key, ct);

    private CreateExpenseResult Replay(Expense expense)
    {
        logger.ExpenseDuplicateRequest(expense.UserId, expense.Id);
        return new CreateExpenseResult(ExpenseDto.From(expense), IsReplay: true);
    }
}
