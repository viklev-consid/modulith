using System.Security.Cryptography;
using System.Text;
using ErrorOr;
using Modulith.Modules.Users.Errors;
using Modulith.Shared.Kernel.Domain;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Domain;

public sealed class PendingTwoFactorChallenge : Entity<PendingTwoFactorChallengeId>, IAuditableEntity
{
    public const int MaxAttempts = 5;

    private PendingTwoFactorChallenge(
        PendingTwoFactorChallengeId id,
        UserId userId,
        byte[] tokenHash,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        string? ipAddress)
        : base(id)
    {
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = issuedAt;
        ExpiresAt = expiresAt;
        IpAddress = ipAddress;
    }

    private PendingTwoFactorChallenge() : base(default!) { }

    public UserId UserId { get; private set; } = null!;
    public byte[] TokenHash { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public int AttemptCount { get; private set; }
    public string? IpAddress { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    public static (PendingTwoFactorChallenge challenge, string rawValue) Create(
        UserId userId,
        TimeSpan lifetime,
        IClock clock,
        string? ipAddress)
    {
        var rawValue = GenerateRawValue();
        var now = clock.UtcNow;
        return (new PendingTwoFactorChallenge(
            PendingTwoFactorChallengeId.New(),
            userId,
            HashRawValue(rawValue),
            now,
            now.Add(lifetime),
            ipAddress), rawValue);
    }

    public bool IsValid(IClock clock) => ConsumedAt is null && ExpiresAt > clock.UtcNow;

    public ErrorOr<Success> RecordFailedAttempt(IClock clock)
    {
        if (!IsValid(clock))
        {
            return UsersErrors.InvalidOrExpiredToken;
        }

        AttemptCount++;
        if (AttemptCount >= MaxAttempts)
        {
            ConsumedAt = clock.UtcNow;
            return UsersErrors.InvalidOrExpiredToken;
        }

        return UsersErrors.TwoFactorCodeInvalid;
    }

    public ErrorOr<Success> Consume(IClock clock)
    {
        if (!IsValid(clock))
        {
            return UsersErrors.InvalidOrExpiredToken;
        }

        ConsumedAt = clock.UtcNow;
        return Result.Success;
    }

    public static byte[] HashRawValue(string rawValue) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(rawValue));

    private static string GenerateRawValue()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');
    }
}
