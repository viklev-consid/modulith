using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Organizations.Contracts.Events;
using Modulith.Modules.Organizations.Errors;
using Modulith.Modules.Organizations.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Organizations.Features.RemoveOrganizationMember;

public sealed class RemoveOrganizationMemberHandler(OrganizationsDbContext db, IClock clock, IMessageBus bus)
{
    public async Task<ErrorOr<Success>> Handle(RemoveOrganizationMemberCommand cmd, CancellationToken ct)
    {
        var organization = await db.Organizations
            .Include(o => o.Memberships)
            .FirstOrDefaultAsync(o => o.Id == cmd.OrganizationId, ct);
        if (organization is null)
        {
            return OrganizationsErrors.OrganizationNotFound;
        }

        var remove = organization.RemoveMember(cmd.UserId, cmd.RemovedByUserId, clock);
        if (remove.IsError)
        {
            return remove.Errors;
        }

        await db.SaveChangesAsync(ct);
        await bus.PublishAsync(new OrganizationMemberRemovedV1(
            organization.Id.Value,
            cmd.UserId,
            cmd.RemovedByUserId,
            Guid.NewGuid()));
        return Result.Success;
    }
}
