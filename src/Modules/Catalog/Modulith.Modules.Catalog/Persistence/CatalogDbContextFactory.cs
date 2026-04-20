using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Modulith.Modules.Catalog.Persistence;

/// <summary>
/// Used only by EF Core tooling (dotnet ef migrations). Not used at runtime.
/// </summary>
public sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CatalogDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=modulith;Username=postgres;Password=postgres",
            b => b.MigrationsHistoryTable("__ef_migrations_history", "catalog"));
        return new CatalogDbContext(optionsBuilder.Options);
    }
}
