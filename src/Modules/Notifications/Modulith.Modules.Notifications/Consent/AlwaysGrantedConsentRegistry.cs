using Modulith.Modules.Notifications.Domain;

namespace Modulith.Modules.Notifications.Consent;

// Phase 8 will replace this with a real implementation backed by the Consents table.
public sealed class AlwaysGrantedConsentRegistry : IConsentRegistry
{
    public bool HasConsented(Guid userId, NotificationType notificationType) => true;
}
