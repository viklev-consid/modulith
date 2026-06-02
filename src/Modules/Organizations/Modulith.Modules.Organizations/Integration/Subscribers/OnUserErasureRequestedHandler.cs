using Modulith.Modules.Organizations.Gdpr;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Shared.Kernel.Gdpr;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine.Attributes;

namespace Modulith.Modules.Organizations.Integration.Subscribers;

[NonTransactional]
public sealed class OnUserErasureRequestedHandler(OrganizationsPersonalDataEraser eraser)
{
    public async Task Handle(UserErasureRequestedV1 @event, CancellationToken ct)
    {
        await eraser.EraseAsync(new UserRef(@event.UserId), ErasureStrategy.Anonymize, ct);
    }
}
