using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Users.Persistence;
using Modulith.TestSupport;

namespace Modulith.Modules.Users.IntegrationTests;

[CollectionDefinition("UsersModule")]
public sealed class UsersModuleCollection : ICollectionFixture<UsersApiFixture> { }

public sealed class UsersApiFixture : ApiTestFixture
{
    protected override async Task MigrateAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<UsersDbContext>();
        await db.Database.MigrateAsync();
    }

    protected override string[] GetSchemasToReset() => ["users"];
}
