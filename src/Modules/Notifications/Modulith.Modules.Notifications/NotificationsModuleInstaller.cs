using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Modules;
using Wolverine;

namespace Modulith.Modules.Notifications;

public sealed class NotificationsModuleInstaller : IModuleInstaller
{
    public string Name => "Notifications";

    public void Install(WebApplicationBuilder builder)
    {
        builder.Services.AddNotificationsModule(builder.Configuration);
    }

    public void ConfigureMessaging(WolverineOptions options)
    {
        options.AddNotificationsHandlers();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapNotificationsEndpoints();
    }
}
