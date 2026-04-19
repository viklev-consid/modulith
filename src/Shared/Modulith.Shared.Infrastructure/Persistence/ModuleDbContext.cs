using Microsoft.EntityFrameworkCore;
using Modulith.Shared.Kernel.Domain;

namespace Modulith.Shared.Infrastructure.Persistence;

public abstract class ModuleDbContext(DbContextOptions options) : DbContext(options)
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.UseSnakeCaseNamingConvention();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Ignore<DomainEvent>();
    }
}
