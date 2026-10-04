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
    IUserDirectory users,
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
                return await Replay(existing, cancellationToken);
        }

        var expense = Expense.Create(userId, user.LedgerId, request.Amount, request.Category, request.Note, request.SpentOn, key);
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
            return await Replay(winner, cancellationToken);
        }

        logger.ExpenseCreated(expense.Id, userId, expense.Amount, expense.SpentOn);
        return new CreateExpenseResult(ExpenseDto.From(expense, await NameOf(userId, cancellationToken)), IsReplay: false);
    }

    // Keys are unique per creator across ledgers (the user may have switched household since), so the ledger filter
    // is bypassed; the explicit UserId condition keeps it to this user's own expenses.
    private Task<Expense?> FindByIdempotencyKey(Guid userId, string key, CancellationToken ct) =>
        db.Expenses.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(e => e.UserId == userId && e.IdempotencyKey == key, ct);

    private async Task<CreateExpenseResult> Replay(Expense expense, CancellationToken ct)
    {
        logger.ExpenseDuplicateRequest(expense.UserId, expense.Id);
        return new CreateExpenseResult(ExpenseDto.From(expense, await NameOf(expense.UserId, ct)), IsReplay: true);
    }

    private async Task<string?> NameOf(Guid userId, CancellationToken ct) =>
        (await users.GetDisplayNamesAsync([userId], ct)).GetValueOrDefault(userId);
}
