using Microsoft.Extensions.Caching.Hybrid;
using Wolverine;

namespace Modulith.Shared.Infrastructure.Messaging;

public sealed class CacheInvalidationMiddleware(HybridCache cache)
{
    public async Task AfterAsync(Envelope envelope, CancellationToken cancellationToken)
    {
        if (envelope.Message is not IInvalidatesCache invalidating) return;

        foreach (var key in invalidating.CacheKeys)
            await cache.RemoveAsync(key, cancellationToken);
    }
}
