using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Wolverine;

namespace Modulith.Shared.Infrastructure.Modules;

public interface IModuleInstaller
{
    string Name { get; }

    void Install(WebApplicationBuilder builder);

    void ConfigureMessaging(WolverineOptions options);

    void MapEndpoints(IEndpointRouteBuilder endpoints);

    void Use(WebApplication app)
    {
    }
}
