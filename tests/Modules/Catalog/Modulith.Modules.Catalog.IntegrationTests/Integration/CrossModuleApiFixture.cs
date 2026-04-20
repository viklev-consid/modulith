using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Catalog.Persistence;
using Modulith.Modules.Users.Persistence;
using Modulith.TestSupport;

namespace Modulith.Modules.Catalog.IntegrationTests.Integration;

[CollectionDefinition("CrossModule")]
public sealed class CrossModuleCollection : ICollectionFixture<CrossModuleApiFixture> { }

public sealed class CrossModuleApiFixture : ApiTestFixture
{
    protected override async Task MigrateAsync(IServiceProvider services)
    {
        var usersDb = services.GetRequiredService<UsersDbContext>();
        await usersDb.Database.MigrateAsync();

        var catalogDb = services.GetRequiredService<CatalogDbContext>();
        await catalogDb.Database.MigrateAsync();
    }

    protected override string[] GetSchemasToReset() => ["users", "catalog"];
}
