using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Modulith.Shared.Infrastructure.Blobs;

namespace Modulith.Shared.Tests.Infrastructure;

[Trait("Category", "Unit")]
public sealed class BlobStorageServiceCollectionExtensionsTests
{
    [Fact]
    public void AddBlobStorage_RegistersLocalDiskBlobStore()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Blob:RootPath"] = Path.Combine(Path.GetTempPath(), $"blobtest-{Guid.NewGuid():N}"),
            ["Blob:SigningKey"] = "test-signing-key-32-chars-long!!!",
        });

        services.AddBlobStorage(configuration, new TestHostEnvironment(Environments.Development));

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var blobStore = provider.GetRequiredService<IBlobStore>();

        Assert.IsType<LocalDiskBlobStore>(blobStore);
    }

    [Fact]
    public void AddBlobStorage_ProductionWithDefaultSigningKey_FailsValidation()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(new Dictionary<string, string?>(StringComparer.Ordinal));

        services.AddBlobStorage(configuration, new TestHostEnvironment(Environments.Production));

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var options = provider.GetRequiredService<IOptions<LocalDiskBlobStoreOptions>>();

        var ex = Assert.Throws<OptionsValidationException>(() => options.Value);
        Assert.Contains("Blob:SigningKey", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddBlobStorage_ShortSigningKey_FailsValidation()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Blob:SigningKey"] = "too-short",
        });

        services.AddBlobStorage(configuration, new TestHostEnvironment(Environments.Development));

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var options = provider.GetRequiredService<IOptions<LocalDiskBlobStoreOptions>>();

        Assert.Throws<OptionsValidationException>(() => options.Value);
    }

    private static IConfiguration CreateConfiguration(IDictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Modulith.Shared.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
