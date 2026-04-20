using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Audit.Domain;
using Modulith.Shared.Infrastructure.Persistence;

namespace Modulith.Modules.Audit.Persistence;

public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : ModuleDbContext(options)
{
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("audit");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditDbContext).Assembly);
    }
}
