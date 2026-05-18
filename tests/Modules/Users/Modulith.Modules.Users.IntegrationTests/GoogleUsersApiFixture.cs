using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Modulith.Modules.Audit.Persistence;
using Modulith.Modules.Catalog.Persistence;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Users.Avatars;
using Modulith.Modules.Users.IntegrationTests.Fakes;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Modulith.Shared.Infrastructure.Notifications;
using Modulith.TestSupport;
using Modulith.TestSupport.Fakes;

namespace Modulith.Modules.Users.IntegrationTests;

[CollectionDefinition("GoogleUsersModule")]
public sealed class GoogleUsersModuleCollection : ICollectionFixture<GoogleUsersApiFixture> { }

[CollectionDefinition("InviteOnlyGoogleUsersModule")]
public sealed class InviteOnlyGoogleUsersModuleCollection : ICollectionFixture<InviteOnlyGoogleUsersApiFixture> { }

public class GoogleUsersApiFixture : ApiTestFixture
{
    public FakeGoogleIdTokenVerifier GoogleVerifier { get; } = new();
    public FakeGoogleAvatarImporter? GoogleAvatarImporter =>
        Services.GetService<IGoogleAvatarImporter>() as FakeGoogleAvatarImporter;

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddSingleton<IGoogleIdTokenVerifier>(GoogleVerifier);
        services.RemoveAll<IGoogleAvatarImporter>();
        services.AddSingleton<IGoogleAvatarImporter, FakeGoogleAvatarImporter>();
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

public sealed class InviteOnlyGoogleUsersApiFixture : GoogleUsersApiFixture
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Modules:Users:Registration:Mode", "InviteOnly");
    }
}
