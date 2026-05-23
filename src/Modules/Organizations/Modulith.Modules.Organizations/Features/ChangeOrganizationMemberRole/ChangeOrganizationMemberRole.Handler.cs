using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Organizations.Contracts.Events;
using Modulith.Modules.Organizations.Domain;
using Modulith.Modules.Organizations.Errors;
using Modulith.Modules.Organizations.Persistence;
using Wolverine;

namespace Modulith.Modules.Organizations.Features.ChangeOrganizationMemberRole;

public sealed class ChangeOrganizationMemberRoleHandler(OrganizationsDbContext db, IMessageBus bus)
{
    public async Task<ErrorOr<ChangeOrganizationMemberRoleResponse>> Handle(ChangeOrganizationMemberRoleCommand cmd, CancellationToken ct)
    {
        var roleResult = OrganizationRole.Create(cmd.Role);
        if (roleResult.IsError)
        {
            return roleResult.Errors;
        }

        var organization = await db.Organizations
            .Include(o => o.Memberships)
            .FirstOrDefaultAsync(o => o.Id == cmd.OrganizationId, ct);
        if (organization is null)
        {
            return OrganizationsErrors.OrganizationNotFound;
        }

        var change = organization.ChangeMemberRole(cmd.ChangedByUserId, cmd.UserId, roleResult.Value);
        if (change.IsError)
        {
            return change.Errors;
        }

        await db.SaveChangesAsync(ct);
        await bus.PublishAsync(new OrganizationMemberRoleChangedV1(
            organization.Id.Value,
            cmd.UserId,
            change.Value,
            roleResult.Value.Name,
            cmd.ChangedByUserId,
            Guid.NewGuid()));

        return new ChangeOrganizationMemberRoleResponse(cmd.UserId, roleResult.Value.Name);
    }
}
