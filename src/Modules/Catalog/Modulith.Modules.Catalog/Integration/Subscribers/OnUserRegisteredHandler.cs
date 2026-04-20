using Modulith.Modules.Catalog.Domain;
using Modulith.Modules.Catalog.Persistence;
using Modulith.Modules.Users.Contracts.Events;

namespace Modulith.Modules.Catalog.Integration.Subscribers;

public sealed class OnUserRegisteredHandler(CatalogDbContext db)
{
    public async Task Handle(UserRegisteredV1 @event, CancellationToken ct)
    {
        var customer = Customer.FromUserRegistered(@event.UserId, @event.Email, @event.DisplayName);
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);
    }
}
