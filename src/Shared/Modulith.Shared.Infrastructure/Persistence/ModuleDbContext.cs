using Microsoft.EntityFrameworkCore;

namespace Modulith.Shared.Infrastructure.Persistence;

public abstract class ModuleDbContext(DbContextOptions options) : DbContext(options)
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.UseSnakeCaseNamingConvention();
    }
}
