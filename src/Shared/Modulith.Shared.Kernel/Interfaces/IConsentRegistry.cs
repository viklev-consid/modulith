namespace Modulith.Shared.Kernel.Interfaces;

public interface IConsentRegistry
{
    Task<bool> HasConsentedAsync(Guid userId, string consentKey, CancellationToken ct = default);
    Task GrantAsync(Guid userId, string consentKey, CancellationToken ct = default);
    Task RevokeAsync(Guid userId, string consentKey, CancellationToken ct = default);
}
