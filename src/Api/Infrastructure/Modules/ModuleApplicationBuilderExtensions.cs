using Modulith.Shared.Infrastructure.Modules;
using Wolverine;

namespace Modulith.Api.Infrastructure.Modules;

internal static class ModuleApplicationBuilderExtensions
{
    public static WebApplicationBuilder InstallModules(
        this WebApplicationBuilder builder,
        IEnumerable<IModuleInstaller> modules)
    {
        foreach (var module in modules)
        {
            module.Install(builder);
        }

        return builder;
    }

    public static WolverineOptions ConfigureModuleMessaging(
        this WolverineOptions options,
        IEnumerable<IModuleInstaller> modules)
    {
        foreach (var module in modules)
        {
            module.ConfigureMessaging(options);
        }

        return options;
    }

    public static IEndpointRouteBuilder MapModuleEndpoints(
        this IEndpointRouteBuilder endpoints,
        IEnumerable<IModuleInstaller> modules)
    {
        foreach (var module in modules)
        {
            module.MapEndpoints(endpoints);
        }

        return endpoints;
    }

    public static WebApplication UseModules(
        this WebApplication app,
        IEnumerable<IModuleInstaller> modules)
    {
        foreach (var module in modules)
        {
            module.Use(app);
        }

        return app;
    }
}
