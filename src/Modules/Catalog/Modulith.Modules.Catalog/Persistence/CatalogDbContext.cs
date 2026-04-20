using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Catalog.Domain;
using Modulith.Shared.Infrastructure.Persistence;

namespace Modulith.Modules.Catalog.Persistence;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : ModuleDbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("catalog");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
    }
}
