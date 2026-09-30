namespace ExpenseTracker.Infrastructure.Time;

public sealed class AppClockOptions
{
    public const string SectionName = "Clock";

    /// <summary>IANA zone that defines "today" when the client does not send its own date.</summary>
    public string TimeZone { get; set; } = "Europe/Istanbul";
}
