using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Modulith.Modules.Organizations.Persistence;

/// <summary>
/// Used only by EF Core tooling (dotnet ef migrations). Not used at runtime.
/// </summary>
public sealed class OrganizationsDbContextFactory : IDesignTimeDbContextFactory<OrganizationsDbContext>
{
    public OrganizationsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrganizationsDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=modulith;Username=postgres;Password=postgres",
            b => b.MigrationsHistoryTable("__ef_migrations_history", "organizations"));
        return new OrganizationsDbContext(optionsBuilder.Options);
    }
}
