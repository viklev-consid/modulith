using Modulith.Modules.Notifications.Domain;

namespace Modulith.Modules.Notifications.Consent;

public interface IConsentRegistry
{
    bool HasConsented(Guid userId, NotificationType notificationType);
}
