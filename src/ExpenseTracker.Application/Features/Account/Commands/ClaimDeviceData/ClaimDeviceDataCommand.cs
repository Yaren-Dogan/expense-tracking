using ExpenseTracker.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Application.Features.Account.Commands.ClaimDeviceData;

/// <summary>
/// Moves data recorded before accounts existed — stored under a random id kept in the browser — into the signed-in
/// user's personal ledger. Ids of registered users and households are refused, so nobody can take over someone
/// else's data by sending their id.
/// </summary>
/// <returns>How many expenses were moved.</returns>
public sealed record ClaimDeviceDataCommand(Guid DeviceUserId) : IRequest<int>;

public sealed class ClaimDeviceDataCommandHandler(
    IAppDbContext db, IUserContext user, IUserDirectory users, ILogger<ClaimDeviceDataCommandHandler> logger)
    : IRequestHandler<ClaimDeviceDataCommand, int>
{
    public async Task<int> Handle(ClaimDeviceDataCommand request, CancellationToken cancellationToken)
    {
        var deviceId = request.DeviceUserId;
        var userId = user.UserId;
        if (deviceId == Guid.Empty || deviceId == userId
            || await users.ExistsAsync(deviceId, cancellationToken)
            || await db.Households.AnyAsync(h => h.Id == deviceId, cancellationToken))
        {
            return 0;
        }

        // The device ledger is not the current ledger, so the ledger filter is bypassed on purpose.
        var expenses = await db.Expenses.IgnoreQueryFilters()
            .Where(e => e.LedgerId == deviceId && e.UserId == deviceId)
            .ToListAsync(cancellationToken);
        foreach (var expense in expenses)
            expense.ReassignTo(userId, userId);

        var deviceBudgets = await db.Budgets.IgnoreQueryFilters().Where(b => b.LedgerId == deviceId).ToListAsync(cancellationToken);
        var ownMonths = (await db.Budgets.IgnoreQueryFilters().Where(b => b.LedgerId == userId)
            .Select(b => new { b.Year, b.Month }).ToListAsync(cancellationToken)).ToHashSet();
        foreach (var budget in deviceBudgets.Where(b => !ownMonths.Contains(new { b.Year, b.Month })))
            budget.MoveToLedger(userId);

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Moved {Count} device expenses to user {UserId}", expenses.Count, userId);
        return expenses.Count;
    }
}
