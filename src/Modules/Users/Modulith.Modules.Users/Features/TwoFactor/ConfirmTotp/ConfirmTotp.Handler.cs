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

namespace Modulith.Modules.Users.Features.TwoFactor.ConfirmTotp;

public sealed class ConfirmTotpHandler(
    UsersDbContext db,
    ITotpService totpService,
    ITotpSecretProtector secretProtector,
    IRefreshTokenRevoker tokenRevoker,
    IOptions<UsersOptions> options,
    IMessageBus bus,
    IClock clock)
{
    public async Task<ErrorOr<ConfirmTotpResponse>> Handle(ConfirmTotpCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(ConfirmTotpHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<ConfirmTotpResponse>> HandleCoreAsync(ConfirmTotpCommand cmd, CancellationToken ct)
    {
        var userId = new UserId(cmd.UserId);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return UsersErrors.UserNotFound;
        }

        var credential = await db.TwoFactorCredentials
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Method == TwoFactorMethod.Totp, ct);

        if (credential is null)
        {
            return UsersErrors.TwoFactorSetupNotFound;
        }

        if (credential.IsEnabled)
        {
            return UsersErrors.TwoFactorAlreadyEnabled;
        }

        var verification = totpService.Verify(
            secretProtector.Unprotect(credential.ProtectedSecret),
            cmd.Code,
            options.Value.TotpAllowedTimeStepDrift);

        if (!verification.IsValid)
        {
            return UsersErrors.TwoFactorCodeInvalid;
        }

        var stepResult = credential.MarkAcceptedTimeStep(verification.TimeStep);
        if (stepResult.IsError)
        {
            return stepResult.Errors;
        }

        var confirmResult = credential.Confirm(clock);
        if (confirmResult.IsError)
        {
            return confirmResult.Errors;
        }

        await db.RecoveryCodes.Where(c => c.UserId == userId).ExecuteDeleteAsync(ct);
        var rawCodes = new List<string>(options.Value.RecoveryCodeCount);
        for (var i = 0; i < options.Value.RecoveryCodeCount; i++)
        {
            var (code, rawValue) = RecoveryCode.Create(userId, clock);
            db.RecoveryCodes.Add(code);
            rawCodes.Add(rawValue);
        }

        RefreshTokenId? keepId = null;
        if (cmd.ActiveRefreshTokenId is not null && Guid.TryParse(cmd.ActiveRefreshTokenId, out var parsed))
        {
            keepId = new RefreshTokenId(parsed);
        }

        await tokenRevoker.RevokeAllForUserAsync(userId, ct, except: keepId);
        await db.SaveChangesAsync(ct);

        await bus.PublishAsync(new TwoFactorEnabledV1(userId.Value, user.Email.Value, TwoFactorMethod.Totp.ToString(), Guid.NewGuid()));
        UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(TwoFactorEnabledV1)));

        return new ConfirmTotpResponse(rawCodes);
    }
}
