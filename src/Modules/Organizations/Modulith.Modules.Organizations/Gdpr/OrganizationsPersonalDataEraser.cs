using Modulith.Shared.Kernel.Gdpr;
using Modulith.Shared.Kernel.Interfaces;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Organizations.Persistence;
using Modulith.Modules.Organizations.Errors;

namespace Modulith.Modules.Organizations.Gdpr;

public sealed class OrganizationsPersonalDataEraser(OrganizationsDbContext db, IClock clock) : IPersonalDataEraser
{
    public async Task<ErasureResult> EraseAsync(UserRef user, ErasureStrategy strategy, CancellationToken ct)
    {
        var recordsAffected = 0;

        var memberships = await db.Memberships
            .Where(m => m.UserId == user.UserId)
            .ToListAsync(ct);

        var activeOrganizationIds = memberships
            .Where(m => m.IsActive)
            .Select(m => m.OrganizationId)
            .Distinct()
            .ToArray();
        var organizationsWithActiveMembership = await db.Organizations
            .Include(o => o.Memberships)
            .Where(o => activeOrganizationIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, ct);

        foreach (var membership in memberships.Where(m => !m.IsActive))
        {
            membership.Anonymize();
            recordsAffected++;
        }

        foreach (var organization in organizationsWithActiveMembership.Values)
        {
            var membership = organization.FindActiveMembership(user.UserId)!;
            var remove = organization.RemoveMember(user.UserId, user.UserId, clock);
            if (remove.IsError)
            {
                throw new InvalidOperationException(
                    $"{OrganizationsErrors.OwnedOrganizationsBlockUserErasure.Code}: {remove.FirstError.Description}");
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

        var deletedOrganizations = await db.Organizations
            .Where(o => o.DeletedByUserId == user.UserId)
            .ToListAsync(ct);

        foreach (var organization in deletedOrganizations)
        {
            organization.AnonymizeUserReferences(user.UserId);
            recordsAffected++;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            db.ChangeTracker.Clear();
            throw new InvalidOperationException(
                $"{OrganizationsErrors.OwnedOrganizationsBlockUserErasure.Code}: Organization ownership changed concurrently.",
                ex);
        }
        return new ErasureResult(user.UserId, strategy, recordsAffected);
    }
}
