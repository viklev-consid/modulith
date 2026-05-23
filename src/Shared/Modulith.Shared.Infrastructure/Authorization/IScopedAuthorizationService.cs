using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Shared.Infrastructure.Authorization;

public interface IScopedAuthorizationService<TScope>
{
    Task<ScopedAuthorizationResult> AuthorizeAsync(
        ICurrentUser currentUser,
        TScope scope,
        string permission,
        ScopedAuthorizationOptions options,
        CancellationToken ct);
}
