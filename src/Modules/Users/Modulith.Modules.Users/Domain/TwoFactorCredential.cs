using ErrorOr;
using Modulith.Modules.Users.Errors;
using Modulith.Shared.Kernel.Domain;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Domain;

public sealed class TwoFactorCredential : Entity<TwoFactorCredentialId>, IAuditableEntity
{
    private TwoFactorCredential(
        TwoFactorCredentialId id,
        UserId userId,
        TwoFactorMethod method,
        string protectedSecret,
        DateTimeOffset createdAt)
        : base(id)
    {
        UserId = userId;
        Method = method;
        ProtectedSecret = protectedSecret;
        CreatedAt = createdAt;
    }

    private TwoFactorCredential() : base(default!) { }

    public UserId UserId { get; private set; } = null!;
    public TwoFactorMethod Method { get; private set; }
    public string ProtectedSecret { get; private set; } = string.Empty;
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public DateTimeOffset? DisabledAt { get; private set; }
    public long? LastAcceptedTimeStep { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    public bool IsEnabled => ConfirmedAt is not null && DisabledAt is null;

    public static ErrorOr<TwoFactorCredential> CreateTotp(UserId userId, string protectedSecret, IClock clock)
    {
        if (string.IsNullOrWhiteSpace(protectedSecret))
        {
            return UsersErrors.TwoFactorSecretInvalid;
        }

        return new TwoFactorCredential(
            TwoFactorCredentialId.New(),
            userId,
            TwoFactorMethod.Totp,
            protectedSecret,
            clock.UtcNow);
    }

    public ErrorOr<Success> ReplaceSecret(string protectedSecret, IClock clock)
    {
        if (IsEnabled)
        {
            return UsersErrors.TwoFactorAlreadyEnabled;
        }

        if (string.IsNullOrWhiteSpace(protectedSecret))
        {
            return UsersErrors.TwoFactorSecretInvalid;
        }

        ProtectedSecret = protectedSecret;
        CreatedAt = clock.UtcNow;
        return Result.Success;
    }

    public ErrorOr<Success> Confirm(IClock clock)
    {
        if (IsEnabled)
        {
            return UsersErrors.TwoFactorAlreadyEnabled;
        }

        ConfirmedAt = clock.UtcNow;
        DisabledAt = null;
        return Result.Success;
    }

    public ErrorOr<Success> Disable(IClock clock)
    {
        if (!IsEnabled)
        {
            return UsersErrors.TwoFactorNotEnabled;
        }

        DisabledAt = clock.UtcNow;
        return Result.Success;
    }

    public ErrorOr<Success> MarkAcceptedTimeStep(long timeStep)
    {
        if (LastAcceptedTimeStep is not null && timeStep <= LastAcceptedTimeStep.Value)
        {
            return UsersErrors.TwoFactorCodeInvalid;
        }

        LastAcceptedTimeStep = timeStep;
        return Result.Success;
    }
}
