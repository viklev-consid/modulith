using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Users.Persistence;
using Modulith.TestSupport;

namespace Modulith.Modules.Users.IntegrationTests;

[CollectionDefinition("UsersModule")]
public sealed class UsersModuleCollection : ICollectionFixture<UsersApiFixture> { }

[CollectionDefinition("InviteOnlyUsersModule")]
public sealed class InviteOnlyUsersModuleCollection : ICollectionFixture<InviteOnlyUsersApiFixture> { }

[CollectionDefinition("RegistrationDisabledUsersModule")]
public sealed class RegistrationDisabledUsersModuleCollection : ICollectionFixture<RegistrationDisabledUsersApiFixture> { }

public class UsersApiFixture : ApiTestFixture
{
    protected override async Task MigrateAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<UsersDbContext>();
        await db.Database.MigrateAsync();
    }

    protected override string[] GetSchemasToReset() => ["users"];
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
