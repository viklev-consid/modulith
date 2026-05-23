using System.Text.Json;
using Modulith.Modules.Organizations.Contracts.Events;
using Wolverine.Attributes;

namespace Modulith.Modules.Audit.Integration.Subscribers;

[NonTransactional]
public sealed class OnOrganizationInvitationCreatedHandler(OrganizationAuditWriter writer)
{
    public async Task Handle(OrganizationInvitationCreatedV1 @event, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new { @event.OrganizationId, @event.InvitationId, @event.Role });
        await writer.WriteAsync(
            "organization.invitation_created",
            @event.InvitedByUserId,
            @event.OrganizationId,
            "OrganizationInvitation",
            @event.InvitationId,
            payload,
            @event.EventId,
            ct);
    }
}
