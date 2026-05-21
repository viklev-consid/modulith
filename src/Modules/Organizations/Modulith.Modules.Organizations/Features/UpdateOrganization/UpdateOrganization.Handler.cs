using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Organizations.Domain;
using Modulith.Modules.Organizations.Errors;
using Modulith.Modules.Organizations.Persistence;
using Modulith.Shared.Infrastructure.Persistence;

namespace Modulith.Modules.Organizations.Features.UpdateOrganization;

public sealed class UpdateOrganizationHandler(OrganizationsDbContext db)
{
    public async Task<ErrorOr<UpdateOrganizationResponse>> Handle(UpdateOrganizationCommand cmd, CancellationToken ct)
    {
        var slugResult = OrganizationSlug.Create(cmd.Slug);
        if (slugResult.IsError)
        {
            return slugResult.Errors;
        }

        var slug = slugResult.Value;
        var organization = await db.Organizations.FirstOrDefaultAsync(o => o.Id == cmd.OrganizationId, ct);
        if (organization is null || organization.IsDeleted)
        {
            return OrganizationsErrors.OrganizationNotFound;
        }

        if (await db.Organizations.AnyAsync(o => o.Slug == slug && o.Id != cmd.OrganizationId, ct))
        {
            return OrganizationsErrors.SlugAlreadyExists;
        }

        var update = organization.Update(cmd.Name, slug);
        if (update.IsError)
        {
            return update.Errors;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            db.ChangeTracker.Clear();
            return OrganizationsErrors.SlugAlreadyExists;
        }

        return new UpdateOrganizationResponse(organization.Id.Value, organization.Name, organization.Slug.Value);
    }
}
