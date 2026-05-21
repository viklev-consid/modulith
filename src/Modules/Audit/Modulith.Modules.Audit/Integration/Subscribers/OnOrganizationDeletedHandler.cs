using System.Text.Json;
using Modulith.Modules.Organizations.Contracts.Events;
using Wolverine.Attributes;

namespace Modulith.Modules.Audit.Integration.Subscribers;

[NonTransactional]
public sealed class OnOrganizationDeletedHandler(OrganizationAuditWriter writer)
{
    public async Task Handle(OrganizationDeletedV1 @event, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new { @event.OrganizationId });
        await writer.WriteAsync(
            "organization.deleted",
            @event.DeletedByUserId,
            @event.OrganizationId,
            "Organization",
            @event.OrganizationId,
            payload,
            @event.EventId,
            ct);
    }
}
