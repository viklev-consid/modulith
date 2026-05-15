using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Security;

public interface ITwoFactorRequirementEvaluator
{
    Task<bool> IsRequiredAsync(User user, CancellationToken ct);
}
