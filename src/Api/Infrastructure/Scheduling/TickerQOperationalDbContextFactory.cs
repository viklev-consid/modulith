using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Modulith.Api.Infrastructure.Scheduling;

/// <summary>
/// Used only by EF Core tooling (dotnet ef migrations). Not used at runtime.
/// </summary>
public sealed class TickerQOperationalDbContextFactory
    : IDesignTimeDbContextFactory<TickerQOperationalDbContext>
{
    public TickerQOperationalDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TickerQOperationalDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=modulith;Username=postgres;Password=postgres",
            b => b.MigrationsHistoryTable("__ef_migrations_history", TickerQOperationalDbContext.Schema));

        return new TickerQOperationalDbContext(optionsBuilder.Options);
    }
}
