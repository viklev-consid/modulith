using System.Text.Json;
using Modulith.Modules.Organizations.Contracts.Events;
using Wolverine.Attributes;

namespace Modulith.Modules.Audit.Integration.Subscribers;

[NonTransactional]
public sealed class OnOrganizationMemberRoleChangedHandler(OrganizationAuditWriter writer)
{
    public async Task Handle(OrganizationMemberRoleChangedV1 @event, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new { @event.OrganizationId, @event.UserId, @event.OldRole, @event.NewRole });
        await writer.WriteAsync(
            "organization.member_role_changed",
            @event.ChangedByUserId,
            @event.OrganizationId,
            "OrganizationMember",
            @event.UserId,
            payload,
            @event.EventId,
            ct);
    }
}
