using System.Security.Cryptography;
using System.Text;
using Modulith.Shared.Kernel.Domain;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Domain;

public sealed class RefreshToken : Entity<RefreshTokenId>
{
    private RefreshToken(
        RefreshTokenId id,
        UserId userId,
        byte[] tokenHash,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        string? deviceFingerprint,
        string? createdFromIp) : base(id)
    {
        UserId = userId;
        TokenHash = tokenHash;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        DeviceFingerprint = deviceFingerprint;
        CreatedFromIp = createdFromIp;
    }

    // Required by EF Core for materialization.
    private RefreshToken() : base(new RefreshTokenId(Guid.Empty)) { }

    public UserId UserId { get; private set; } = null!;
    public byte[] TokenHash { get; private set; } = [];
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public RefreshTokenId? RotatedTo { get; private set; }
    public string? DeviceFingerprint { get; private set; }
    public string? CreatedFromIp { get; private set; }

    /// <summary>
    /// Issues a new refresh token. Returns the entity (to persist) and the raw token
    /// value (to return to the client — never stored after issuance).
    /// </summary>
    public static (RefreshToken token, string rawValue) Issue(
        UserId userId,
        TimeSpan lifetime,
        IClock clock,
        string? userAgent,
        string? ipAddress)
    {
        var rawBytes = RandomNumberGenerator.GetBytes(32); // 256 bits
        var rawValue = Convert.ToBase64String(rawBytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawValue));

        string? fingerprint = null;
        if (userAgent is not null || ipAddress is not null)
        {
            var fpInput = $"{userAgent}|{ipAddress}";
            fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fpInput)));
        }

        var now = clock.UtcNow;
        var token = new RefreshToken(
            RefreshTokenId.New(),
            userId,
            hash,
            now,
            now.Add(lifetime),
            fingerprint,
            ipAddress);

        return (token, rawValue);
    }

    /// <summary>
    /// Revokes this token (single-session logout or security event).
    /// Idempotent — revoking an already-revoked token is a no-op.
    /// </summary>
    public void Revoke(IClock clock)
    {
        if (RevokedAt is null)
            RevokedAt = clock.UtcNow;
    }

    /// <summary>
    /// Marks this token as rotated, linking it to the replacement token.
    /// Used during the refresh flow so reuse detection can walk the chain.
    /// </summary>
    public void MarkRotatedTo(RefreshTokenId newTokenId, IClock clock)
    {
        RevokedAt = clock.UtcNow;
        RotatedTo = newTokenId;
    }

    public bool IsActive(IClock clock) =>
        RevokedAt is null && ExpiresAt > clock.UtcNow;

    /// <summary>
    /// Computes the SHA-256 hash of a raw refresh token value for database lookup.
    /// </summary>
    public static byte[] HashRawValue(string rawValue) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(rawValue));
}
