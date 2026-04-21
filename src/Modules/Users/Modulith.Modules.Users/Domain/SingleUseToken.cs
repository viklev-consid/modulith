using System.Security.Cryptography;
using System.Text;
using Modulith.Modules.Users.Errors;
using Modulith.Shared.Kernel.Domain;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Domain;

public sealed class SingleUseToken : Entity<SingleUseTokenId>
{
    private SingleUseToken(
        SingleUseTokenId id,
        UserId userId,
        byte[] tokenHash,
        TokenPurpose purpose,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt) : base(id)
    {
        UserId = userId;
        TokenHash = tokenHash;
        Purpose = purpose;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
    }

    // Required by EF Core for materialization.
    private SingleUseToken() : base(new SingleUseTokenId(Guid.Empty)) { }

    public UserId UserId { get; private set; } = null!;
    public byte[] TokenHash { get; private set; } = [];
    public TokenPurpose Purpose { get; private set; }
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }

    /// <summary>
    /// Creates a new single-use token. Returns the entity (to persist) and the raw
    /// token value (to send to the user — never stored).
    /// </summary>
    public static (SingleUseToken token, string rawValue) Create(
        UserId userId,
        TokenPurpose purpose,
        TimeSpan lifetime,
        IClock clock)
    {
        var rawBytes = RandomNumberGenerator.GetBytes(32); // 256 bits
        var rawValue = Convert.ToBase64String(rawBytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawValue));

        var now = clock.UtcNow;
        var token = new SingleUseToken(
            SingleUseTokenId.New(),
            userId,
            hash,
            purpose,
            now,
            now.Add(lifetime));

        return (token, rawValue);
    }

    /// <summary>
    /// Marks this token as consumed. Fails if the token is already consumed or expired.
    /// </summary>
    public ErrorOr.ErrorOr<ErrorOr.Success> Consume(IClock clock)
    {
        if (!IsValid(clock))
        {
            return UsersErrors.InvalidOrExpiredToken;
        }

        ConsumedAt = clock.UtcNow;
        return ErrorOr.Result.Success;
    }

    public bool IsValid(IClock clock) =>
        ConsumedAt is null && ExpiresAt > clock.UtcNow;

    /// <summary>
    /// Computes the SHA-256 hash of a raw token value for database lookup.
    /// </summary>
    public static byte[] HashRawValue(string rawValue) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(rawValue));
}
