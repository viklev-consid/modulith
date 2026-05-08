using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Modulith.Shared.Infrastructure.Modules;
using Wolverine;

namespace Modulith.Modules.Users;

public sealed class UsersModuleInstaller : IModuleInstaller
{
    public string Name => "Users";

    public void Install(WebApplicationBuilder builder)
    {
        builder.Services.AddUsersModule(builder.Configuration, builder.Environment);
    }

    public void ConfigureMessaging(WolverineOptions options)
    {
        options.AddUsersHandlers();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapUsersEndpoints();
    }
}
