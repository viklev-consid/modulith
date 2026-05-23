using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Organizations.Domain;
using Modulith.Modules.Organizations.Errors;
using Modulith.Modules.Organizations.Persistence;

namespace Modulith.Modules.Organizations;

public sealed record OrganizationRef(OrganizationId Id, string Name, string Slug);

public interface IOrganizationRefResolver
{
    Task<ErrorOr<OrganizationRef>> ResolveAsync(string organizationRef, CancellationToken ct);
}

internal sealed class OrganizationRefResolver(OrganizationsDbContext db) : IOrganizationRefResolver
{
    public async Task<ErrorOr<OrganizationRef>> ResolveAsync(string organizationRef, CancellationToken ct)
    {
        Organization? organization;
        if (Guid.TryParse(organizationRef, out var id))
        {
            var organizationId = new OrganizationId(id);
            organization = await db.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == organizationId, ct);
        }
        else
        {
            var slugResult = OrganizationSlug.Create(organizationRef);
            if (slugResult.IsError)
            {
                return slugResult.Errors;
            }

            var slug = slugResult.Value;
            organization = await db.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Slug == slug, ct);
        }

        if (organization is null || organization.IsDeleted)
        {
            return OrganizationsErrors.OrganizationNotFound;
        }

        return new OrganizationRef(organization.Id, organization.Name, organization.Slug.Value);
    }
}
