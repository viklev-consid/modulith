using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Audit.Persistence;
using Modulith.Modules.Catalog.Persistence;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Organizations.Persistence;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Infrastructure.Notifications;
using Modulith.TestSupport;
using Modulith.TestSupport.Fakes;

namespace Modulith.Modules.Audit.IntegrationTests;

[CollectionDefinition("AuditCrossModule")]
public sealed class AuditCrossModuleCollection : ICollectionFixture<AuditCrossModuleFixture> { }

public sealed class AuditCrossModuleFixture : ApiTestFixture
{
    public async Task ConfirmEmailAsync(string email)
    {
        await ExecuteDbAsync<UsersDbContext>(async (db, ct) =>
        {
            var clock = Services.GetRequiredService<Modulith.Shared.Kernel.Interfaces.IClock>();
            var user = await db.Users.FirstAsync(u => u.Email == Email.Create(email).Value, ct);
            user.ConfirmEmail(clock);
            await db.SaveChangesAsync(ct);
        });
    }

    // Suppress SMTP dial attempts from the Notifications handler.
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddSingleton<IEmailSender, FakeEmailSender>();
    }

    protected override async Task MigrateAsync(IServiceProvider services)
    {
        await services.GetRequiredService<UsersDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<AuditDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<NotificationsDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<OrganizationsDbContext>().Database.MigrateAsync();
    }

    protected override string[] GetSchemasToReset() => ["users", "catalog", "audit", "notifications", "organizations"];
}
