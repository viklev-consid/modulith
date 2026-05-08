using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Catalog.Domain;
using Modulith.Modules.Catalog.Persistence;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Shared.Infrastructure.Persistence;
using Wolverine.Attributes;

namespace Modulith.Modules.Catalog.Integration.Subscribers;

[NonTransactional]
public sealed class OnUserRegisteredHandler(CatalogDbContext db)
{
    public async Task Handle(UserRegisteredV1 @event, CancellationToken ct)
    {
        using var activity = CatalogTelemetry.ActivitySource.StartActivity(nameof(OnUserRegisteredHandler));
        CatalogTelemetry.EventsProcessed.Add(1, new KeyValuePair<string, object?>("event", nameof(UserRegisteredV1)));

        var customer = Customer.FromUserRegistered(@event.UserId, @event.Email, @event.DisplayName);
        db.Customers.Add(customer);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            // Idempotency: duplicate delivery — customer already exists, nothing to do.
        }
    }
}
