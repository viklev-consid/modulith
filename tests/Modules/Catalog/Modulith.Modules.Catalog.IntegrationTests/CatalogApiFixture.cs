using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Catalog.Persistence;
using Modulith.TestSupport;

namespace Modulith.Modules.Catalog.IntegrationTests;

[CollectionDefinition("CatalogModule")]
public sealed class CatalogModuleCollection : ICollectionFixture<CatalogApiFixture> { }

public sealed class CatalogApiFixture : ApiTestFixture
{
    protected override async Task MigrateAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<CatalogDbContext>();
        await db.Database.MigrateAsync();
    }

    protected override string[] GetSchemasToReset() => ["catalog"];
}
