using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Audit.Persistence;
using Modulith.Modules.Catalog.Persistence;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Users.IntegrationTests.Fakes;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Modulith.TestSupport;

namespace Modulith.Modules.Users.IntegrationTests;

[CollectionDefinition("GoogleUsersModule")]
public sealed class GoogleUsersModuleCollection : ICollectionFixture<GoogleUsersApiFixture> { }

public sealed class GoogleUsersApiFixture : ApiTestFixture
{
    public FakeGoogleIdTokenVerifier GoogleVerifier { get; } = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddSingleton<IGoogleIdTokenVerifier>(GoogleVerifier);
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
