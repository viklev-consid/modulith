using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.TwoFactor.DisableTwoFactor;

public sealed class DisableTwoFactorHandler(
    UsersDbContext db,
    IPasswordHasher passwordHasher,
    ITotpService totpService,
    ITotpSecretProtector secretProtector,
    IRefreshTokenRevoker tokenRevoker,
    IOptions<UsersOptions> options,
    IMessageBus bus,
    IClock clock)
{
    public async Task<ErrorOr<DisableTwoFactorResponse>> Handle(DisableTwoFactorCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(DisableTwoFactorHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<DisableTwoFactorResponse>> HandleCoreAsync(DisableTwoFactorCommand cmd, CancellationToken ct)
    {
        var userId = new UserId(cmd.UserId);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return UsersErrors.UserNotFound;
        }

        if (user.PasswordHash is null || !passwordHasher.Verify(cmd.CurrentPassword, user.PasswordHash.Value))
        {
            return UsersErrors.CurrentPasswordIncorrect;
        }

        var credential = await db.TwoFactorCredentials
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Method == TwoFactorMethod.Totp, ct);
        if (credential is null || !credential.IsEnabled)
        {
            return UsersErrors.TwoFactorNotEnabled;
        }

        var verificationResult = await VerifyCodeAsync(userId, credential, cmd.Code, ct);
        if (verificationResult.IsError)
        {
            return verificationResult.Errors;
        }

        var disableResult = credential.Disable(clock);
        if (disableResult.IsError)
        {
            return disableResult.Errors;
        }

        await db.RecoveryCodes.Where(c => c.UserId == userId).ExecuteDeleteAsync(ct);

        RefreshTokenId? keepId = null;
        if (cmd.ActiveRefreshTokenId is not null && Guid.TryParse(cmd.ActiveRefreshTokenId, out var parsed))
        {
            keepId = new RefreshTokenId(parsed);
        }

        await tokenRevoker.RevokeAllForUserAsync(userId, ct, except: keepId);
        await db.SaveChangesAsync(ct);

        await bus.PublishAsync(new TwoFactorDisabledV1(userId.Value, user.Email.Value, TwoFactorMethod.Totp.ToString(), Guid.NewGuid()));
        UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(TwoFactorDisabledV1)));

        return new DisableTwoFactorResponse();
    }

    private async Task<ErrorOr<Success>> VerifyCodeAsync(
        UserId userId,
        TwoFactorCredential credential,
        string code,
        CancellationToken ct)
    {
        if (IsRecoveryCode(code))
        {
            var hash = RecoveryCode.HashRawValue(NormalizeRecoveryCode(code));
            var recoveryCode = await db.RecoveryCodes.FirstOrDefaultAsync(c =>
                c.UserId == userId &&
                c.CodeHash == hash &&
                c.ConsumedAt == null,
                ct);

            if (recoveryCode is null)
            {
                return UsersErrors.RecoveryCodeInvalid;
            }

            return Result.Success;
        }

        var verification = totpService.Verify(
            secretProtector.Unprotect(credential.ProtectedSecret),
            code,
            options.Value.TotpPreviousStepGracePeriod);

        if (!verification.IsValid)
        {
            return UsersErrors.TwoFactorCodeInvalid;
        }

        return credential.MarkAcceptedTimeStep(verification.TimeStep);
    }

    private static bool IsRecoveryCode(string code)
    {
        var parts = code.Split('-', StringSplitOptions.None);
        return parts.Length == 4
            && parts.All(p => p.Length == 5 && p.All(Uri.IsHexDigit));
    }

    private static string NormalizeRecoveryCode(string code) =>
        code.ToLowerInvariant();
}
