using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Wolverine;

namespace Modulith.Modules.Users.Features.ForgotPassword;

public sealed class ForgotPasswordHandler(
    UsersDbContext db,
    ISingleUseTokenService tokenService,
    IOptions<UsersOptions> options,
    IMessageBus bus)
{
    public async Task<ErrorOr<ForgotPasswordResponse>> Handle(ForgotPasswordCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(ForgotPasswordHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<ForgotPasswordResponse>> HandleCoreAsync(ForgotPasswordCommand cmd, CancellationToken ct)
    {
        var emailResult = Email.Create(cmd.Email);

        // Always return the same response regardless of whether the email exists.
        if (emailResult.IsError)
        {
            return new ForgotPasswordResponse();
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == emailResult.Value, ct);

        if (user is not null)
        {
            var (_, rawToken) = tokenService.Create(
                user.Id,
                TokenPurpose.PasswordReset,
                options.Value.PasswordResetTokenLifetime);

            await db.SaveChangesAsync(ct);

            await bus.PublishAsync(new PasswordResetRequestedV1(
                user.Id.Value,
                user.Email.Value,
                rawToken));
            UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(PasswordResetRequestedV1)));
        }

        return new ForgotPasswordResponse();
    }
}
