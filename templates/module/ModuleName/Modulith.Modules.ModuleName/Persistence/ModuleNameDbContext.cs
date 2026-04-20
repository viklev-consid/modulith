using Microsoft.EntityFrameworkCore;
using Modulith.Shared.Infrastructure.Persistence;

namespace Modulith.Modules.ModuleName.Persistence;

public sealed class ModuleNameDbContext(DbContextOptions<ModuleNameDbContext> options) : ModuleDbContext(options)
{
    // TODO: public DbSet<YourEntity> YourEntities => Set<YourEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("modulenamelower");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ModuleNameDbContext).Assembly);
    }
}
