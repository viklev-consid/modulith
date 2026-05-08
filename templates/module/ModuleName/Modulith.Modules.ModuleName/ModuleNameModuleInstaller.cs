using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Modules;
using Wolverine;

namespace Modulith.Modules.ModuleName;

public sealed class ModuleNameModuleInstaller : IModuleInstaller
{
    public string Name => "ModuleName";

    public void Install(WebApplicationBuilder builder)
    {
        builder.Services.AddModuleNameModule(builder.Configuration, builder.Environment);
    }

    public void ConfigureMessaging(WolverineOptions options)
    {
        options.AddModuleNameHandlers();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapModuleNameEndpoints();
    }
}
