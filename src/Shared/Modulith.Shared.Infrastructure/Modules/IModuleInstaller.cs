using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using TickerQ.Utilities;
using TickerQ.Utilities.Entities;
using Wolverine;

namespace Modulith.Shared.Infrastructure.Modules;

public interface IModuleInstaller
{
    string Name { get; }

    void Install(WebApplicationBuilder builder);

    void ConfigureMessaging(WolverineOptions options);

    void ConfigureJobs(TickerOptionsBuilder<TimeTickerEntity, CronTickerEntity> options)
    {
    }

    void MapEndpoints(IEndpointRouteBuilder endpoints);

    void Use(WebApplication app)
    {
    }
}
