using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Infrastructure.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.CreateInvitation;

public sealed class CreateInvitationHandler(
    UsersDbContext db,
    IOptions<UsersOptions> options,
    IMessageBus bus,
    IClock clock)
{
    public async Task<ErrorOr<CreateInvitationResponse>> Handle(CreateInvitationCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(CreateInvitationHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<CreateInvitationResponse>> HandleCoreAsync(CreateInvitationCommand cmd, CancellationToken ct)
    {
        var emailResult = Email.Create(cmd.Email);
        if (emailResult.IsError)
        {
            return emailResult.Errors;
        }

        var email = emailResult.Value;

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            return UsersErrors.EmailAlreadyRegistered;
        }

        await db.UserInvitations
            .Where(i => i.Email == email.Value && i.IsPending && i.ExpiresAt <= clock.UtcNow)
            .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.IsPending, false), ct);

        var inviteResult = UserInvitation.Create(
            email,
            options.Value.Registration.InvitationTokenLifetime,
            clock,
            new UserId(cmd.InvitedByUserId),
            cmd.IpAddress,
            cmd.UserAgent);

        if (inviteResult.IsError)
        {
            return inviteResult.Errors;
        }

        var (invitation, rawToken) = inviteResult.Value;
        db.UserInvitations.Add(invitation);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            db.ChangeTracker.Clear();
            return UsersErrors.InvitationAlreadyExists;
        }

        await bus.PublishAsync(new UserInvitationCreatedV1(
            invitation.Id.Value,
            invitation.Email,
            rawToken,
            invitation.ExpiresAt,
            cmd.InvitedByUserId,
            Guid.NewGuid()));
        UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(UserInvitationCreatedV1)));

        return new CreateInvitationResponse(invitation.Id.Value, invitation.Email, rawToken, invitation.ExpiresAt);
    }
}
