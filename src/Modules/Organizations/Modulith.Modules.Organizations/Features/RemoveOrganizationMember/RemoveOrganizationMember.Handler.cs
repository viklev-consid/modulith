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

        var remove = organization.RemoveMemberAsActor(cmd.RemovedByUserId, cmd.UserId, clock);
        if (remove.IsError)
        {
            return remove.Errors;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return OrganizationsErrors.ConcurrencyConflict;
        }
        await bus.PublishAsync(new OrganizationMemberRemovedV1(
            organization.Id.Value,
            cmd.UserId,
            cmd.RemovedByUserId,
            Guid.NewGuid()));
        return Result.Success;
    }
}
