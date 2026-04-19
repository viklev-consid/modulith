namespace Modulith.Shared.Infrastructure.Seeding;

public interface IModuleSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
