using Microsoft.FeatureManagement.FeatureFilters;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Api.Infrastructure.FeatureFlags;

internal sealed class CurrentUserTargetingContextAccessor(ICurrentUser currentUser) : ITargetingContextAccessor
{
    public ValueTask<TargetingContext> GetContextAsync() =>
        new(new TargetingContext { UserId = currentUser.Id });
}
