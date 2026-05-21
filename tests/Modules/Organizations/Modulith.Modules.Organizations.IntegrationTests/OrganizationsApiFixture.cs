using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Audit.Persistence;
using Modulith.Modules.Organizations.Persistence;
using Modulith.Modules.Users.Persistence;
using Modulith.TestSupport;

namespace Modulith.Modules.Organizations.IntegrationTests;

[CollectionDefinition("OrganizationsModule")]
public sealed class OrganizationsModuleCollection : ICollectionFixture<OrganizationsApiFixture> { }

[CollectionDefinition("InviteOnlyOrganizationsModule")]
public sealed class InviteOnlyOrganizationsModuleCollection : ICollectionFixture<InviteOnlyOrganizationsApiFixture> { }

public class OrganizationsApiFixture : ApiTestFixture
{
    protected override async Task MigrateAsync(IServiceProvider services)
    {
        await services.GetRequiredService<UsersDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<OrganizationsDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<AuditDbContext>().Database.MigrateAsync();
    }

    protected override string[] GetSchemasToReset() => ["users", "organizations", "audit"];
}

public sealed class InviteOnlyOrganizationsApiFixture : OrganizationsApiFixture
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Modules:Users:Registration:Mode", "InviteOnly");
    }
}
