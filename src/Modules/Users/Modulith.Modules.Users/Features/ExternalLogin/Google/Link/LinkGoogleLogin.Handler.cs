using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Modulith.Shared.Kernel.Interfaces;
using Npgsql;
using Wolverine;

namespace Modulith.Modules.Users.Features.ExternalLogin.Google.Link;

public sealed class LinkGoogleLoginHandler(
    UsersDbContext db,
    IGoogleIdTokenVerifier verifier,
    IMessageBus bus,
    IClock clock)
{
    public async Task<ErrorOr<Success>> Handle(LinkGoogleLoginCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(LinkGoogleLoginHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<Success>> HandleCoreAsync(LinkGoogleLoginCommand cmd, CancellationToken ct)
    {
        var identityResult = await verifier.VerifyAsync(cmd.IdToken, ct);
        if (identityResult.IsError)
        {
            return identityResult.Errors;
        }

        var identity = identityResult.Value;

        var user = await db.Users
            .Include(u => u.ExternalLogins)
            .FirstOrDefaultAsync(u => u.Id == new UserId(cmd.UserId), ct);

        if (user is null)
        {
            return UsersErrors.UserNotFound;
        }

        var now = clock.UtcNow;
        var linkResult = user.LinkExternalLogin(ExternalLoginProvider.Google, identity.Subject, now);
        if (linkResult.IsError)
        {
            return linkResult.Errors;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            return UsersErrors.ExternalLoginLinkedToOtherUser;
        }

        await bus.PublishAsync(new ExternalLoginLinkedV1(user.Id.Value, user.Email.Value, "Google", identity.Subject, now, Guid.NewGuid()));
        UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(ExternalLoginLinkedV1)));

        return Result.Success;
    }
}
