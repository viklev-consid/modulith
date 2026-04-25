using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        var db = services.GetRequiredService<UsersDbContext>();
        await db.Database.MigrateAsync();
    }

    protected override string[] GetSchemasToReset() => ["users"];
}
