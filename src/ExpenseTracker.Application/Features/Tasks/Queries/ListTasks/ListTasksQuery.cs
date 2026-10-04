using ExpenseTracker.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Features.Tasks.Queries.ListTasks;

public sealed record ListTasksQuery : IRequest<TaskListDto>;

public sealed class ListTasksQueryHandler(IAppDbContext db, IUserContext user, IUserDirectory users)
    : IRequestHandler<ListTasksQuery, TaskListDto>
{
    private const int DoneLimit = 20;

    public async Task<TaskListDto> Handle(ListTasksQuery request, CancellationToken cancellationToken)
    {
        var ledgerId = user.LedgerId;
        var open = await db.Tasks.AsNoTracking()
            .Where(t => t.LedgerId == ledgerId && t.CompletedAt == null)
            .OrderBy(t => t.DueOn).ThenBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
        var done = await db.Tasks.AsNoTracking()
            .Where(t => t.LedgerId == ledgerId && t.CompletedAt != null)
            .OrderByDescending(t => t.CompletedAt)
            .Take(DoneLimit)
            .ToListAsync(cancellationToken);

        var mentioned = open.Concat(done)
            .SelectMany(t => new[] { t.AssigneeId, t.CreatedById, t.CompletedById ?? t.CreatedById });
        var people = await LedgerPeople.LoadAsync(db, user, users, cancellationToken, mentioned);

        return new TaskListDto(
            open.Select(t => people.ToDto(t, user.UserId)).ToList(),
            done.Select(t => people.ToDto(t, user.UserId)).ToList());
    }
}
