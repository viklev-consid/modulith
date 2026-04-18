using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Modulith.Shared.Infrastructure.Blobs;

public sealed class LocalDiskBlobStore(IOptions<LocalDiskBlobStoreOptions> options) : IBlobStore
{
    private readonly string _rootPath = options.Value.RootPath;
    private readonly byte[] _signingKey = Encoding.UTF8.GetBytes(options.Value.SigningKey);

    public async Task<BlobRef> PutAsync(Stream content, BlobMetadata metadata, CancellationToken ct)
    {
        var key = Guid.NewGuid().ToString("N");
        var container = metadata.FileName is not null
            ? SanitizeContainer(Path.GetExtension(metadata.FileName).TrimStart('.'))
            : "files";

        var dir = Path.Combine(_rootPath, container);
        Directory.CreateDirectory(dir);

        await using var fileStream = File.Create(GetFilePath(container, key));
        await content.CopyToAsync(fileStream, ct);

        var metaJson = JsonSerializer.Serialize(metadata);
        await File.WriteAllTextAsync(GetMetaPath(container, key), metaJson, ct);

        return new BlobRef(container, key);
    }

    public async Task<BlobContent> GetAsync(BlobRef reference, CancellationToken ct)
    {
        var filePath = GetFilePath(reference.Container, reference.Key);
        var metaPath = GetMetaPath(reference.Container, reference.Key);

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Blob not found: {reference.Container}/{reference.Key}");

        var metaJson = await File.ReadAllTextAsync(metaPath, ct);
        var metadata = JsonSerializer.Deserialize<BlobMetadata>(metaJson)
            ?? throw new InvalidOperationException("Corrupted blob metadata.");

        var stream = File.OpenRead(filePath);
        return new BlobContent(stream, metadata);
    }

    public Task DeleteAsync(BlobRef reference, CancellationToken ct)
    {
        var filePath = GetFilePath(reference.Container, reference.Key);
        var metaPath = GetMetaPath(reference.Container, reference.Key);

        if (File.Exists(filePath)) File.Delete(filePath);
        if (File.Exists(metaPath)) File.Delete(metaPath);

        return Task.CompletedTask;
    }

    public Task<Uri> GetDownloadUrlAsync(BlobRef reference, TimeSpan lifetime, CancellationToken ct)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(lifetime).ToUnixTimeSeconds();
        var token = ComputeToken(reference.Container, reference.Key, expiresAt);
        var url = new Uri(
            $"/blobs/{Uri.EscapeDataString(reference.Container)}/{Uri.EscapeDataString(reference.Key)}" +
            $"?token={Uri.EscapeDataString(token)}&exp={expiresAt}",
            UriKind.Relative);

        return Task.FromResult(url);
    }

    private string ComputeToken(string container, string key, long expiresAt)
    {
        var message = $"{container}:{key}:{expiresAt}";
        using var hmac = new HMACSHA256(_signingKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private string GetFilePath(string container, string key) =>
        Path.Combine(_rootPath, container, key);

    private string GetMetaPath(string container, string key) =>
        Path.Combine(_rootPath, container, key + ".meta.json");

    private static string SanitizeContainer(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "files";
        return string.Concat(value.Where(c => char.IsLetterOrDigit(c) || c == '-')).ToLowerInvariant();
    }
}
