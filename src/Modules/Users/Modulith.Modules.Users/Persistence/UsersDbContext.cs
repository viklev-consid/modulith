using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Domain;
using Modulith.Shared.Infrastructure.Persistence;

namespace Modulith.Modules.Users.Persistence;

public sealed class UsersDbContext(DbContextOptions<UsersDbContext> options) : ModuleDbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Consent> Consents => Set<Consent>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SingleUseToken> SingleUseTokens => Set<SingleUseToken>();
    public DbSet<PendingEmailChange> PendingEmailChanges => Set<PendingEmailChange>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<PendingExternalLogin> PendingExternalLogins => Set<PendingExternalLogin>();
    public DbSet<TermsAcceptance> TermsAcceptances => Set<TermsAcceptance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("users");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UsersDbContext).Assembly);
    }
}
