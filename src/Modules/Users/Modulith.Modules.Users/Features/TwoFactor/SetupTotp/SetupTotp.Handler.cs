using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Features.TwoFactor.SetupTotp;

public sealed class SetupTotpHandler(
    UsersDbContext db,
    ITotpService totpService,
    ITotpSecretProtector secretProtector,
    IOptions<UsersOptions> options,
    IClock clock)
{
    public async Task<ErrorOr<SetupTotpResponse>> Handle(SetupTotpCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(SetupTotpHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<SetupTotpResponse>> HandleCoreAsync(SetupTotpCommand cmd, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == new UserId(cmd.UserId), ct);
        if (user is null)
        {
            return UsersErrors.UserNotFound;
        }

        var secret = totpService.GenerateSecret();
        var protectedSecret = secretProtector.Protect(secret);
        var credential = await db.TwoFactorCredentials
            .FirstOrDefaultAsync(c => c.UserId == user.Id && c.Method == TwoFactorMethod.Totp, ct);

        if (credential is null)
        {
            var createResult = TwoFactorCredential.CreateTotp(user.Id, protectedSecret, clock);
            if (createResult.IsError)
            {
                return createResult.Errors;
            }

            db.TwoFactorCredentials.Add(createResult.Value);
        }
        else
        {
            var replaceResult = credential.ReplaceSecret(protectedSecret, clock);
            if (replaceResult.IsError)
            {
                return replaceResult.Errors;
            }
        }

        await db.SaveChangesAsync(ct);
        var uri = totpService.BuildOtpAuthUri(options.Value.TotpIssuer, user.Email.Value, secret);
        return new SetupTotpResponse(secret, uri);
    }
}
