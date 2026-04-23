using Microsoft.Extensions.Options;
using Modulith.Shared.Infrastructure.Blobs;

namespace Modulith.Shared.Tests.Infrastructure;

[Trait("Category", "Unit")]
public sealed class LocalDiskBlobStoreTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"blobtest-{Guid.NewGuid():N}");
    private readonly LocalDiskBlobStore _store;

    public LocalDiskBlobStoreTests()
    {
        var options = Options.Create(new LocalDiskBlobStoreOptions
        {
            RootPath = _tempDir,
            SigningKey = "test-signing-key-32-chars-long!!!",
        });
        _store = new LocalDiskBlobStore(options);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task PutAsync_StoresFileAndMetadata()
    {
        var content = new MemoryStream("hello world"u8.ToArray());
        var metadata = new BlobMetadata("text/plain", 11, "test.txt");

        var blobRef = await _store.PutAsync(content, metadata, CancellationToken.None);

        Assert.NotNull(blobRef);
        Assert.NotEmpty(blobRef.Key);
        Assert.NotEmpty(blobRef.Container);
    }

    [Fact]
    public async Task GetAsync_ReturnsStoredContent()
    {
        var bytes = "hello world"u8.ToArray();
        var content = new MemoryStream(bytes);
        var metadata = new BlobMetadata("text/plain", bytes.Length, "test.txt");

        var blobRef = await _store.PutAsync(content, metadata, CancellationToken.None);
        var retrieved = await _store.GetAsync(blobRef, CancellationToken.None);

        await using var stream = retrieved.Stream;
        var buffer = new byte[bytes.Length];
        var read = await stream.ReadAsync(buffer);

        Assert.Equal(bytes.Length, read);
        Assert.Equal(bytes, buffer);
        Assert.Equal("text/plain", retrieved.Metadata.ContentType);
        Assert.Equal("test.txt", retrieved.Metadata.FileName);
    }

    [Fact]
    public async Task DeleteAsync_RemovesFileAndMetadata()
    {
        var content = new MemoryStream("data"u8.ToArray());
        var metadata = new BlobMetadata("text/plain", 4, null);
        var blobRef = await _store.PutAsync(content, metadata, CancellationToken.None);

        await _store.DeleteAsync(blobRef, CancellationToken.None);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _store.GetAsync(blobRef, CancellationToken.None));
    }

    [Fact]
    public async Task GetDownloadUrlAsync_ReturnsRelativeUriWithToken()
    {
        var content = new MemoryStream("test"u8.ToArray());
        var metadata = new BlobMetadata("text/plain", 4, null);
        var blobRef = await _store.PutAsync(content, metadata, CancellationToken.None);

        var url = await _store.GetDownloadUrlAsync(blobRef, TimeSpan.FromMinutes(5), CancellationToken.None);

        Assert.False(url.IsAbsoluteUri);
        Assert.Contains("/blobs/", url.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("token=", url.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exp=", url.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PutAsync_MultipleFiles_StoredIndependently()
    {
        var ref1 = await _store.PutAsync(
            new MemoryStream("file1"u8.ToArray()),
            new BlobMetadata("text/plain", 5, null),
            CancellationToken.None);

        var ref2 = await _store.PutAsync(
            new MemoryStream("file2"u8.ToArray()),
            new BlobMetadata("text/plain", 5, null),
            CancellationToken.None);

        Assert.NotEqual(ref1.Key, ref2.Key);

        var content1 = await _store.GetAsync(ref1, CancellationToken.None);
        var content2 = await _store.GetAsync(ref2, CancellationToken.None);

        await using var s1 = content1.Stream;
        await using var s2 = content2.Stream;

        var buf1 = new byte[5];
        var buf2 = new byte[5];
        await s1.ReadExactlyAsync(buf1);
        await s2.ReadExactlyAsync(buf2);

        Assert.Equal("file1"u8.ToArray(), buf1);
        Assert.Equal("file2"u8.ToArray(), buf2);
    }
}
