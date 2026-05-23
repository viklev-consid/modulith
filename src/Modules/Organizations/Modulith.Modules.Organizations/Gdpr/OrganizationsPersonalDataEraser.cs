using Modulith.Shared.Kernel.Gdpr;
using Modulith.Shared.Kernel.Interfaces;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Organizations.Persistence;

namespace Modulith.Modules.Organizations.Gdpr;

public sealed class OrganizationsPersonalDataEraser(OrganizationsDbContext db, IClock clock) : IPersonalDataEraser
{
    public async Task<ErasureResult> EraseAsync(UserRef user, ErasureStrategy strategy, CancellationToken ct)
    {
        var recordsAffected = 0;

        var memberships = await db.Memberships
            .Where(m => m.UserId == user.UserId)
            .ToListAsync(ct);

        foreach (var membership in memberships)
        {
            if (membership.IsActive)
            {
                membership.Remove(user.UserId, clock);
            }

            membership.Anonymize();
            recordsAffected++;
        }

        var invitations = await db.Invitations
            .Where(i => i.InvitedByUserId == user.UserId ||
                i.AcceptedUserId == user.UserId ||
                i.RevokedByUserId == user.UserId)
            .ToListAsync(ct);

        foreach (var invitation in invitations)
        {
            invitation.AnonymizeUserReferences(user.UserId);
            recordsAffected++;
        }

        var organizations = await db.Organizations
            .Where(o => o.DeletedByUserId == user.UserId)
            .ToListAsync(ct);

        foreach (var organization in organizations)
        {
            organization.AnonymizeUserReferences(user.UserId);
            recordsAffected++;
        }

        await db.SaveChangesAsync(ct);
        return new ErasureResult(user.UserId, strategy, recordsAffected);
    }
}
