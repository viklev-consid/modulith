using System.Security.Cryptography;
using System.Text;
using ErrorOr;
using Modulith.Modules.Users.Errors;
using Modulith.Shared.Kernel.Domain;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Domain;

public sealed class RecoveryCode : Entity<RecoveryCodeId>, IAuditableEntity
{
    private RecoveryCode(
        RecoveryCodeId id,
        UserId userId,
        byte[] codeHash,
        DateTimeOffset createdAt)
        : base(id)
    {
        UserId = userId;
        CodeHash = codeHash;
        CreatedAt = createdAt;
    }

    private RecoveryCode() : base(default!) { }

    public UserId UserId { get; private set; } = null!;
    public byte[] CodeHash { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    public bool IsActive => ConsumedAt is null;

    public static (RecoveryCode code, string rawValue) Create(UserId userId, IClock clock)
    {
        var rawValue = GenerateRawValue();
        return (new RecoveryCode(RecoveryCodeId.New(), userId, HashRawValue(rawValue), clock.UtcNow), rawValue);
    }

    public ErrorOr<Success> Consume(IClock clock)
    {
        if (ConsumedAt is not null)
        {
            return UsersErrors.RecoveryCodeInvalid;
        }

        ConsumedAt = clock.UtcNow;
        return Result.Success;
    }

    public static byte[] HashRawValue(string rawValue) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(rawValue));

    private static string GenerateRawValue()
    {
        Span<byte> bytes = stackalloc byte[10];
        RandomNumberGenerator.Fill(bytes);
        var text = Convert.ToHexString(bytes).ToLowerInvariant();
        return $"{text[..5]}-{text[5..10]}-{text[10..15]}-{text[15..]}";
    }
}
