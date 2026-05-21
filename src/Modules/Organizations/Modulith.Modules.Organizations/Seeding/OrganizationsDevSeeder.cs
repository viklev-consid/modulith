using Modulith.Shared.Infrastructure.Seeding;

namespace Modulith.Modules.Organizations.Seeding;

internal sealed class OrganizationsDevSeeder : IModuleSeeder
{
    public Task SeedAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
