using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Modules;
using Wolverine;

namespace Modulith.Modules.Organizations;

public sealed class OrganizationsModuleInstaller : IModuleInstaller
{
    public string Name => "Organizations";

    public void Install(WebApplicationBuilder builder)
    {
        builder.Services.AddOrganizationsModule(builder.Configuration, builder.Environment);
    }

    public void ConfigureMessaging(WolverineOptions options)
    {
        options.AddOrganizationsHandlers();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapOrganizationsEndpoints();
    }
}
