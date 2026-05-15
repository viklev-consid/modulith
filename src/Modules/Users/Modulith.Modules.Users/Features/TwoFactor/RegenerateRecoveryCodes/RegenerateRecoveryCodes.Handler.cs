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

namespace Modulith.Modules.Users.Features.TwoFactor.RegenerateRecoveryCodes;

public sealed class RegenerateRecoveryCodesHandler(
    UsersDbContext db,
    IPasswordHasher passwordHasher,
    ITotpService totpService,
    ITotpSecretProtector secretProtector,
    IOptions<UsersOptions> options,
    IMessageBus bus,
    IClock clock)
{
    public async Task<ErrorOr<RegenerateRecoveryCodesResponse>> Handle(RegenerateRecoveryCodesCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(RegenerateRecoveryCodesHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<RegenerateRecoveryCodesResponse>> HandleCoreAsync(RegenerateRecoveryCodesCommand cmd, CancellationToken ct)
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

        await db.RecoveryCodes.Where(c => c.UserId == userId).ExecuteDeleteAsync(ct);
        var rawCodes = new List<string>(options.Value.RecoveryCodeCount);
        for (var i = 0; i < options.Value.RecoveryCodeCount; i++)
        {
            var (code, rawValue) = RecoveryCode.Create(userId, clock);
            db.RecoveryCodes.Add(code);
            rawCodes.Add(rawValue);
        }

        await db.SaveChangesAsync(ct);
        await bus.PublishAsync(new RecoveryCodesRegeneratedV1(userId.Value, user.Email.Value, Guid.NewGuid()));
        UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(RecoveryCodesRegeneratedV1)));

        return new RegenerateRecoveryCodesResponse(rawCodes);
    }
}
