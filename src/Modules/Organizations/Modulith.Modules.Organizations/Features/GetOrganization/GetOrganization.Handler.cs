using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Organizations.Errors;
using Modulith.Modules.Organizations.Persistence;

namespace Modulith.Modules.Organizations.Features.GetOrganization;

public sealed class GetOrganizationHandler(OrganizationsDbContext db)
{
    public async Task<ErrorOr<GetOrganizationResponse>> Handle(GetOrganizationQuery query, CancellationToken ct)
    {
        var organization = await db.Organizations
            .AsNoTracking()
            .Where(o => o.Id == query.OrganizationId && !o.IsDeleted)
            .Select(o => new GetOrganizationResponse(o.Id.Value, o.Name, o.Slug.Value))
            .FirstOrDefaultAsync(ct);

        return organization is null ? OrganizationsErrors.OrganizationNotFound : organization;
    }
}
