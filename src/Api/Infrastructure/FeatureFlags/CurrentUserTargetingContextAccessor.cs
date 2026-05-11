using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.FeatureManagement.FeatureFilters;

namespace Modulith.Api.Infrastructure.FeatureFlags;

internal sealed class CurrentUserTargetingContextAccessor(IHttpContextAccessor httpContextAccessor) : ITargetingContextAccessor
{
    public ValueTask<TargetingContext> GetContextAsync() =>
        new(new TargetingContext { UserId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) });
}
