using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Wolverine;

namespace Modulith.Modules.Users.Features.ResendEmailConfirmation;

public sealed class ResendEmailConfirmationHandler(
    UsersDbContext db,
    ISingleUseTokenService tokenService,
    IOptions<UsersOptions> options,
    IMessageBus bus)
{
    public async Task<ErrorOr<ResendEmailConfirmationResponse>> Handle(ResendEmailConfirmationCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(ResendEmailConfirmationHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<ResendEmailConfirmationResponse>> HandleCoreAsync(ResendEmailConfirmationCommand cmd, CancellationToken ct)
    {
        var emailResult = Email.Create(cmd.Email);
        if (emailResult.IsError)
        {
            return new ResendEmailConfirmationResponse();
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == emailResult.Value, ct);
        if (user is null || user.IsEmailConfirmed)
        {
            return new ResendEmailConfirmationResponse();
        }

        await db.SingleUseTokens
            .Where(t => t.UserId == user.Id
                && t.Purpose == TokenPurpose.EmailConfirmation
                && t.ConsumedAt == null)
            .ExecuteDeleteAsync(ct);

        var (_, rawToken) = tokenService.Create(
            user.Id,
            TokenPurpose.EmailConfirmation,
            options.Value.EmailConfirmationTokenLifetime);

        await db.SaveChangesAsync(ct);

        await bus.PublishAsync(new EmailConfirmationRequestedV1(
            user.Id.Value,
            user.Email.Value,
            user.DisplayName,
            rawToken,
            Guid.NewGuid()));
        UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(EmailConfirmationRequestedV1)));

        return new ResendEmailConfirmationResponse();
    }
}
