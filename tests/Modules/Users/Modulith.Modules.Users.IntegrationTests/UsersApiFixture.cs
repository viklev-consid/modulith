using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Modulith.Modules.Audit.Persistence;
using Modulith.Modules.Catalog.Persistence;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Organizations.Persistence;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Infrastructure.Notifications;
using Modulith.Shared.Kernel.Interfaces;
using Modulith.TestSupport;
using Modulith.TestSupport.Fakes;

namespace Modulith.Modules.Users.IntegrationTests;

[CollectionDefinition("UsersModule")]
public sealed class UsersModuleCollection : ICollectionFixture<UsersApiFixture> { }

[CollectionDefinition("InviteOnlyUsersModule")]
public sealed class InviteOnlyUsersModuleCollection : ICollectionFixture<InviteOnlyUsersApiFixture> { }

[CollectionDefinition("RegistrationDisabledUsersModule")]
public sealed class RegistrationDisabledUsersModuleCollection : ICollectionFixture<RegistrationDisabledUsersApiFixture> { }

public class UsersApiFixture : ApiTestFixture
{
    public TestClock Clock { get; } = new();

    public async Task ConfirmEmailAsync(string email)
    {
        await ExecuteDbAsync<UsersDbContext>(async (db, ct) =>
        {
            var clock = Services.GetRequiredService<IClock>();
            var user = await db.Users.FirstAsync(u => u.Email == Email.Create(email).Value, ct);
            user.ConfirmEmail(clock);
            await db.SaveChangesAsync(ct);
        });
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<IClock>();
        services.AddSingleton<IClock>(Clock);
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

public sealed class InviteOnlyUsersApiFixture : UsersApiFixture
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Modules:Users:Registration:Mode", "InviteOnly");
    }
}

public sealed class RegistrationDisabledUsersApiFixture : UsersApiFixture
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Modules:Users:Registration:Mode", "Disabled");
    }
}

internal static class UsersApiFixtureEmailConfirmationExtensions
{
    public static async Task ConfirmEmailAsync(this ApiTestFixture fixture, string email)
    {
        await fixture.ExecuteDbAsync<UsersDbContext>(async (db, ct) =>
        {
            var clock = fixture.Services.GetRequiredService<IClock>();
            var user = await db.Users.FirstAsync(u => u.Email == Email.Create(email).Value, ct);
            user.ConfirmEmail(clock);
            await db.SaveChangesAsync(ct);
        });
    }
}
