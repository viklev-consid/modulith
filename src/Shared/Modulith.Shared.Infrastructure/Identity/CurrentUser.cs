using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Shared.Infrastructure.Identity;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private const string PermissionClaimType = "permission";

    public string? Id =>
        httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? Name =>
        httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name);

    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public string? ActiveRefreshTokenId =>
        httpContextAccessor.HttpContext?.User?.FindFirstValue("rtid");

    public string? Role =>
        httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Role);

    private IReadOnlyCollection<string>? _permissions;

    public IReadOnlyCollection<string> Permissions =>
        _permissions ??= httpContextAccessor.HttpContext?.User?
            .FindAll(PermissionClaimType)
            .Select(c => c.Value)
            .ToArray() ?? [];

    public bool HasPermission(string permission) =>
        httpContextAccessor.HttpContext?.User?
            .HasClaim(PermissionClaimType, permission) ?? false;
}
