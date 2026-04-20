using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Audit.Persistence;
using Modulith.Modules.Catalog.Persistence;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Infrastructure.Notifications;
using Modulith.TestSupport;
using Modulith.TestSupport.Fakes;

namespace Modulith.Modules.Users.IntegrationTests;

[CollectionDefinition("UsersGdpr")]
public sealed class UsersGdprCollection : ICollectionFixture<GdprApiFixture> { }

public sealed class GdprApiFixture : ApiTestFixture
{
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
    }

    protected override string[] GetSchemasToReset() => ["users", "catalog", "audit", "notifications"];
}
