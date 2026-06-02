using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Catalog.Persistence;
using Modulith.Modules.Users.Contracts.Events;
using Wolverine.Attributes;

namespace Modulith.Modules.Catalog.Integration.Subscribers;

[NonTransactional]
public sealed class OnUserProfileUpdatedHandler(CatalogDbContext db)
{
    public async Task Handle(UserProfileUpdatedV1 @event, CancellationToken ct)
    {
        using var activity = CatalogTelemetry.ActivitySource.StartActivity(nameof(OnUserProfileUpdatedHandler));
        CatalogTelemetry.EventsProcessed.Add(1, new KeyValuePair<string, object?>("event", nameof(UserProfileUpdatedV1)));

        await db.Customers
            .Where(c => c.UserId == @event.UserId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.DisplayName, @event.NewDisplayName), ct);
    }
}
