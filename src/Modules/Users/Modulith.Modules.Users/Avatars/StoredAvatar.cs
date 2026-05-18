using Modulith.Shared.Infrastructure.Blobs;

namespace Modulith.Modules.Users.Avatars;

public sealed record StoredAvatar(BlobRef BlobRef, string ContentType, long SizeBytes, DateTimeOffset StoredAt);
