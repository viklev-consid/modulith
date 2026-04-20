using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Modulith.Modules.Users.Persistence;

/// <summary>
/// Used only by EF Core tooling (dotnet ef migrations). Not used at runtime.
/// </summary>
public sealed class UsersDbContextFactory : IDesignTimeDbContextFactory<UsersDbContext>
{
    public UsersDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UsersDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=modulith;Username=postgres;Password=postgres",
            b => b.MigrationsHistoryTable("__ef_migrations_history", "users"));
        return new UsersDbContext(optionsBuilder.Options);
    }
}
