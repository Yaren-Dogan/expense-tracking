using Microsoft.AspNetCore.Identity;

namespace ExpenseTracker.Infrastructure.Identity;

/// <summary>An account. Lives in Infrastructure: the domain only knows users by id, never by credentials.</summary>
public sealed class AppUser : IdentityUser<Guid>
{
    public const int DisplayNameMaxLength = 40;

    public AppUser()
    {
        // Time-ordered like every other key, and never Guid.Empty.
        Id = Guid.CreateVersion7();
    }

    public string DisplayName { get; set; } = string.Empty;
}
