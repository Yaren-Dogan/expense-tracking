namespace ExpenseTracker.Application.Common.Interfaces;

public interface IUserContext
{
    /// <summary>The caller's id. Guaranteed non-empty inside a request that passed the user middleware.</summary>
    Guid UserId { get; }
}
