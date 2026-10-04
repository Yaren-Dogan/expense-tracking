namespace ExpenseTracker.Application.Common.Interfaces;

/// <summary>Read-only access to user accounts, which live in the identity store outside the domain model.</summary>
public interface IUserDirectory
{
    Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken);

    /// <summary>True when the id belongs to a registered account (used to stop anyone claiming another user's data).</summary>
    Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken);
}
