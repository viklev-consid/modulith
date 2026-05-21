using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Organizations.Domain;
using Modulith.Shared.Infrastructure.Persistence;

namespace Modulith.Modules.Organizations.Persistence;

public sealed class OrganizationsDbContext(DbContextOptions<OrganizationsDbContext> options) : ModuleDbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMembership> Memberships => Set<OrganizationMembership>();
    public DbSet<OrganizationInvitation> Invitations => Set<OrganizationInvitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("organizations");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrganizationsDbContext).Assembly);
    }
}
