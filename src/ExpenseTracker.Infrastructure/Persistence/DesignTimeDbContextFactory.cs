using ExpenseTracker.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ExpenseTracker.Infrastructure.Persistence;

/// <summary>Lets <c>dotnet ef</c> build the model without an HTTP request or a running database.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=expense_tracker", npgsql => npgsql.UseVector())
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options, new NoUser(), TimeProvider.System);
    }

    private sealed class NoUser : IUserContext
    {
        public Guid UserId => Guid.Empty;
        public Guid LedgerId => Guid.Empty;
    }
}
