using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Common.Logging;
using ExpenseTracker.Domain.Enums;
using ExpenseTracker.Domain.Notifications;
using ExpenseTracker.Domain.Tasks;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Application.Features.Tasks.Commands.CreateTask;

public sealed record CreateTaskCommand(
    string Title,
    Guid AssigneeId,
    DateOnly DueOn,
    TaskRecurrence Recurrence,
    ExpenseCategory Category) : IRequest<TaskDto>;

public sealed class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskCommandValidator(IAppClock clock)
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(HouseholdTask.TitleMaxLength);
        RuleFor(x => x.AssigneeId).NotEmpty().WithMessage("Görevi kime vereceğini seç.");
        // A year back allows recording an overdue bill; two years ahead covers yearly payments planned early.
        RuleFor(x => x.DueOn)
            .Must(d => d >= clock.Today.AddYears(-1) && d <= clock.Today.AddYears(2))
            .WithMessage("Son gün bir yıl öncesi ile iki yıl sonrası arasında olmalı.");
        RuleFor(x => x.Recurrence).IsInEnum();
        RuleFor(x => x.Category).IsInEnum();
    }
}

public sealed class CreateTaskCommandHandler(
    IAppDbContext db,
    IUserContext user,
    IUserDirectory users,
    ILogger<CreateTaskCommandHandler> logger)
    : IRequestHandler<CreateTaskCommand, TaskDto>
{
    public async Task<TaskDto> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
    {
        var people = await LedgerPeople.LoadAsync(db, user, users, cancellationToken);
        if (!people.Members.Contains(request.AssigneeId))
            throw new ValidationException([new ValidationFailure(nameof(request.AssigneeId), "Görev sadece hanendeki birine verilebilir.")]);

        var task = HouseholdTask.Create(
            user.LedgerId, request.Title, request.Category, request.AssigneeId, user.UserId, request.DueOn, request.Recurrence);
        db.Tasks.Add(task);

        // Nobody needs to be told about a task they just gave themselves.
        if (task.AssigneeId != user.UserId)
            db.Notifications.Add(Notification.Create(
                task.AssigneeId, NotificationKind.TaskAssigned, task.Id, TaskMessages.Assigned(people.NameOf(user.UserId), task)));

        await db.SaveChangesAsync(cancellationToken);

        logger.TaskCreated(task.Id, task.LedgerId, task.AssigneeId);
        return people.ToDto(task, user.UserId);
    }
}
