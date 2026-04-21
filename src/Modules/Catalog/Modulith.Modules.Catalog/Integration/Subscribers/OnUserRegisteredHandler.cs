using Modulith.Modules.Catalog.Domain;
using Modulith.Modules.Catalog.Persistence;
using Modulith.Modules.Users.Contracts.Events;

namespace Modulith.Modules.Catalog.Integration.Subscribers;

public sealed class OnUserRegisteredHandler(CatalogDbContext db)
{
    public async Task Handle(UserRegisteredV1 @event, CancellationToken ct)
    {
        using var activity = CatalogTelemetry.ActivitySource.StartActivity(nameof(OnUserRegisteredHandler));
        CatalogTelemetry.EventsProcessed.Add(1, new KeyValuePair<string, object?>("event", nameof(UserRegisteredV1)));

        var customer = Customer.FromUserRegistered(@event.UserId, @event.Email, @event.DisplayName);
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);
    }
}
