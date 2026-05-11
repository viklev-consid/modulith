using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.Features.RevokeInvitation;

public sealed class RevokeInvitationHandler(UsersDbContext db, IClock clock)
{
    public async Task<ErrorOr<RevokeInvitationResponse>> Handle(RevokeInvitationCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(RevokeInvitationHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<RevokeInvitationResponse>> HandleCoreAsync(RevokeInvitationCommand cmd, CancellationToken ct)
    {
        var invitation = await db.UserInvitations
            .FirstOrDefaultAsync(i => i.Id == new UserInvitationId(cmd.InvitationId), ct);

        if (invitation is null)
        {
            return UsersErrors.UserNotFound;
        }

        var revokeResult = invitation.Revoke(clock);
        if (revokeResult.IsError)
        {
            return revokeResult.Errors;
        }

        await db.SaveChangesAsync(ct);

        return new RevokeInvitationResponse(invitation.Id.Value, invitation.Email, invitation.RevokedAt!.Value);
    }
}
