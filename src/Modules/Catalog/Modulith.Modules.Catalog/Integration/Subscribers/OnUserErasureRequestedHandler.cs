using Modulith.Modules.Catalog.Gdpr;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Shared.Kernel.Gdpr;
using Wolverine.Attributes;

namespace Modulith.Modules.Catalog.Integration.Subscribers;

[NonTransactional]
public sealed class OnUserErasureRequestedHandler(CatalogPersonalDataEraser eraser)
{
    public async Task Handle(UserErasureRequestedV1 @event, CancellationToken ct)
    {
        using var activity = CatalogTelemetry.ActivitySource.StartActivity(nameof(OnUserErasureRequestedHandler));
        CatalogTelemetry.EventsProcessed.Add(1, new KeyValuePair<string, object?>("event", nameof(UserErasureRequestedV1)));

        var userRef = new UserRef(@event.UserId, @event.DisplayName);
        await eraser.EraseAsync(userRef, ErasureStrategy.Anonymize, ct);
    }
}
