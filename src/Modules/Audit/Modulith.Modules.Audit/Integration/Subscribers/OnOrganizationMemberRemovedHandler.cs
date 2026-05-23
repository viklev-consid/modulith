using System.Text.Json;
using Modulith.Modules.Organizations.Contracts.Events;
using Wolverine.Attributes;

namespace Modulith.Modules.Audit.Integration.Subscribers;

[NonTransactional]
public sealed class OnOrganizationMemberRemovedHandler(OrganizationAuditWriter writer)
{
    public async Task Handle(OrganizationMemberRemovedV1 @event, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new { @event.OrganizationId, @event.UserId });
        await writer.WriteAsync(
            "organization.member_removed",
            @event.RemovedByUserId,
            @event.OrganizationId,
            "OrganizationMember",
            @event.UserId,
            payload,
            @event.EventId,
            ct);
    }
}
