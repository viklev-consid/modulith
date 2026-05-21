using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Organizations.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine.Attributes;

namespace Modulith.Modules.Organizations.Integration.Subscribers;

[NonTransactional]
public sealed class OnUserErasureRequestedHandler(OrganizationsDbContext db, IClock clock)
{
    public async Task Handle(UserErasureRequestedV1 @event, CancellationToken ct)
    {
        var memberships = await db.Memberships
            .Where(m => m.UserId == @event.UserId && m.IsActive)
            .ToListAsync(ct);

        foreach (var membership in memberships)
        {
            membership.Remove(@event.UserId, clock);
            membership.Anonymize();
        }

        var historicalMemberships = await db.Memberships
            .Where(m => m.UserId == @event.UserId && !m.IsAnonymized)
            .ToListAsync(ct);

        foreach (var membership in historicalMemberships)
        {
            membership.Anonymize();
        }

        await db.SaveChangesAsync(ct);
    }
}
