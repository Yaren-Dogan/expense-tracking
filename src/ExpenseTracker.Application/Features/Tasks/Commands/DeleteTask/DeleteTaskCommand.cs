using ExpenseTracker.Application.Common.Exceptions;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Common.Logging;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Application.Features.Tasks.Commands.DeleteTask;

/// <summary>Deleting a monthly task's open occurrence ends the series: the next month is only opened on completion.</summary>
/// <returns>False when the task does not exist in the current ledger.</returns>
public sealed record DeleteTaskCommand(Guid Id) : IRequest<bool>;

public sealed class DeleteTaskCommandHandler(
    IAppDbContext db,
    IUserContext user,
    IUserDirectory users,
    ILogger<DeleteTaskCommandHandler> logger)
    : IRequestHandler<DeleteTaskCommand, bool>
{
    public async Task<bool> Handle(DeleteTaskCommand request, CancellationToken cancellationToken)
    {
        var ledgerId = user.LedgerId;
        var task = await db.Tasks.SingleOrDefaultAsync(t => t.Id == request.Id && t.LedgerId == ledgerId, cancellationToken);
        if (task is null)
            return false;

        var people = await LedgerPeople.LoadAsync(db, user, users, cancellationToken);
        if (!people.CanDelete(task, user.UserId))
            throw new ForbiddenException("Görevi sadece veren kişi ya da hane kurucusu silebilir.");

        db.Tasks.Remove(task);
        await db.SaveChangesAsync(cancellationToken);

        logger.TaskDeleted(task.Id, user.UserId);
        return true;
    }
}
