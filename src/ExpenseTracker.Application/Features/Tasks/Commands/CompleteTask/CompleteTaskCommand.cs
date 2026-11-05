using ExpenseTracker.Application.Common.Exceptions;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Common.Logging;
using ExpenseTracker.Domain.Entities;
using ExpenseTracker.Domain.Notifications;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Application.Features.Tasks.Commands.CompleteTask;

/// <param name="Amount">When given, the payment is recorded as an expense in the task's category and ledger.</param>
/// <returns>Null when the task does not exist in the current ledger.</returns>
public sealed record CompleteTaskCommand(Guid Id, decimal? Amount) : IRequest<CompleteTaskResult?>;

public sealed class CompleteTaskCommandValidator : AbstractValidator<CompleteTaskCommand>
{
    public CompleteTaskCommandValidator()
    {
        RuleFor(x => x.Amount!.Value)
            .GreaterThan(0)
            .LessThanOrEqualTo(Expense.MaxAmount)
            .PrecisionScale(9, 2, ignoreTrailingZeros: true)
            .OverridePropertyName(nameof(CompleteTaskCommand.Amount))
            .When(x => x.Amount is not null);
    }
}

public sealed class CompleteTaskCommandHandler(
    IAppDbContext db,
    IUserContext user,
    IUserDirectory users,
    IAppClock clock,
    TimeProvider time,
    ILogger<CompleteTaskCommandHandler> logger)
    : IRequestHandler<CompleteTaskCommand, CompleteTaskResult?>
{
    private const string AlreadyDone = "Bu görev zaten tamamlandı.";

    public async Task<CompleteTaskResult?> Handle(CompleteTaskCommand request, CancellationToken cancellationToken)
    {
        var (userId, ledgerId) = (user.UserId, user.LedgerId);
        var task = await db.Tasks.SingleOrDefaultAsync(t => t.Id == request.Id && t.LedgerId == ledgerId, cancellationToken);
        if (task is null)
            return null;
        if (task.IsDone)
            throw new ConflictException(AlreadyDone);
        if (!task.CanBeCompletedBy(userId))
            throw new ForbiddenException("Görevi sadece atanan kişi ya da görevi veren tamamlayabilir.");

        // The bill was the assignee's job, so the payment is their expense even when the person who gave the task ticks
        // it off for them: it shows under their name and they can delete it.
        var payment = request.Amount is { } amount
            ? Expense.Create(task.AssigneeId, ledgerId, amount, task.Category, task.Title, clock.Today, idempotencyKey: null)
            : null;
        if (payment is not null)
            db.Expenses.Add(payment);

        var next = task.Complete(userId, time.GetUtcNow(), payment);
        if (next is not null)
            db.Tasks.Add(next);

        var people = await LedgerPeople.LoadAsync(db, user, users, cancellationToken, [task.AssigneeId, task.CreatedById]);
        var message = TaskMessages.Completed(people.NameOf(userId), task);
        // Tell whoever else is involved: the person who asked for it, and the assignee if someone did it for them. Only
        // people still sharing this ledger: a giver who has left keeps no window onto the household's bills and payments.
        foreach (var recipient in new[] { task.CreatedById, task.AssigneeId }.Distinct()
                     .Where(id => id != userId && people.Members.Contains(id)))
            db.Notifications.Add(Notification.Create(recipient, NotificationKind.TaskCompleted, task.Id, message));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            // Two people pressed "done" at once: the other request already opened next month's task.
            throw new ConflictException(AlreadyDone);
        }

        logger.TaskCompleted(task.Id, userId, payment?.Id);
        return new CompleteTaskResult(people.ToDto(task, userId), next is null ? null : people.ToDto(next, userId));
    }
}
