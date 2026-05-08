using Modulith.Shared.Infrastructure.Seeding;

namespace Modulith.Modules.ModuleName.Seeding;

internal sealed class ModuleNameDevSeeder : IModuleSeeder
{
    public Task SeedAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
