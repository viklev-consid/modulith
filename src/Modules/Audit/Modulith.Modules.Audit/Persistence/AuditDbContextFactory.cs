using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Modulith.Modules.Audit.Persistence;

/// <summary>
/// Used only by EF Core tooling (dotnet ef migrations). Not used at runtime.
/// </summary>
public sealed class AuditDbContextFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AuditDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=modulith;Username=postgres;Password=postgres",
            b => b.MigrationsHistoryTable("__ef_migrations_history", "audit"));
        return new AuditDbContext(optionsBuilder.Options);
    }
}
