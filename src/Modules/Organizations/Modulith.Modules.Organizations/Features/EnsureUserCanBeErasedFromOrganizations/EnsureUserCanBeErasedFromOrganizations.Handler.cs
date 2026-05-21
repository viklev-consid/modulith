using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Organizations.Contracts.Commands;
using Modulith.Modules.Organizations.Domain;
using Modulith.Modules.Organizations.Errors;
using Modulith.Modules.Organizations.Persistence;

namespace Modulith.Modules.Organizations.Features.EnsureUserCanBeErasedFromOrganizations;

public sealed class EnsureUserCanBeErasedFromOrganizationsHandler(OrganizationsDbContext db)
{
    public async Task<ErrorOr<Success>> Handle(EnsureUserCanBeErasedFromOrganizationsCommand cmd, CancellationToken ct)
    {
        var ownsActiveOrganization = await db.Memberships
            .AsNoTracking()
            .Where(m => m.UserId == cmd.UserId && m.IsActive && m.Role == OrganizationRole.Owner)
            .Join(
                db.Organizations.AsNoTracking().Where(o => !o.IsDeleted),
                membership => membership.OrganizationId,
                organization => organization.Id,
                (_, _) => true)
            .AnyAsync(ct);

        return ownsActiveOrganization
            ? OrganizationsErrors.OwnedOrganizationsBlockUserErasure
            : Result.Success;
    }
}
