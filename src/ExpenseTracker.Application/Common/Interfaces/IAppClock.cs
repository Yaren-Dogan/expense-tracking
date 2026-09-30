namespace ExpenseTracker.Application.Common.Interfaces;

public interface IAppClock
{
    /// <summary>Today's date in the application's configured time zone, not UTC.</summary>
    DateOnly Today { get; }
}
