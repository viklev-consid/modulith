using System.Text.Json;
using Modulith.Modules.Organizations.Contracts.Events;
using Wolverine.Attributes;

namespace Modulith.Modules.Audit.Integration.Subscribers;

[NonTransactional]
public sealed class OnOrganizationMemberAddedHandler(OrganizationAuditWriter writer)
{
    public async Task Handle(OrganizationMemberAddedV1 @event, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new { @event.OrganizationId, @event.UserId, @event.Role });
        await writer.WriteAsync(
            "organization.member_added",
            @event.UserId,
            @event.OrganizationId,
            "OrganizationMember",
            @event.UserId,
            payload,
            @event.EventId,
            ct);
    }
}
