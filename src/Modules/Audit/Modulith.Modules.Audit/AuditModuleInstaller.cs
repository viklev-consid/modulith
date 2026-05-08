using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Modules;
using Wolverine;

namespace Modulith.Modules.Audit;

public sealed class AuditModuleInstaller : IModuleInstaller
{
    public string Name => "Audit";

    public void Install(WebApplicationBuilder builder)
    {
        builder.Services.AddAuditModule(builder.Configuration);
    }

    public void ConfigureMessaging(WolverineOptions options)
    {
        options.AddAuditHandlers();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAuditEndpoints();
    }
}
