using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Modulith.Shared.Infrastructure.Blobs;

public static class BlobStorageServiceCollectionExtensions
{
    public static IServiceCollection AddBlobStorage(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<LocalDiskBlobStoreOptions>()
            .Bind(configuration.GetSection("Blob"))
            .ValidateDataAnnotations()
            .Validate(
                opts => !string.IsNullOrWhiteSpace(opts.RootPath),
                "Blob:RootPath must be configured.")
            .Validate(
                opts => !string.IsNullOrWhiteSpace(opts.SigningKey),
                "Blob:SigningKey must be configured.")
            .Validate(
                opts => !environment.IsProduction()
                    || !string.Equals(
                        opts.SigningKey,
                        LocalDiskBlobStoreOptions.DefaultSigningKey,
                        StringComparison.Ordinal),
                "Blob:SigningKey must be configured with a production secret.")
            .ValidateOnStart();

        services.AddSingleton<IBlobStore, LocalDiskBlobStore>();

        return services;
    }
}
