using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Catalog.Persistence;
using Modulith.Modules.Users.Persistence;
using Modulith.TestSupport;

namespace Modulith.Modules.Catalog.IntegrationTests;

[CollectionDefinition("CatalogModule")]
public sealed class CatalogModuleCollection : ICollectionFixture<CatalogApiFixture> { }

public sealed class CatalogApiFixture : ApiTestFixture
{
    protected override async Task MigrateAsync(IServiceProvider services)
    {
        await services.GetRequiredService<UsersDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
    }

    protected override string[] GetSchemasToReset() => ["users", "catalog"];
}
