using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Modulith.Shared.Infrastructure.Frontend;

public static class FrontendServiceCollectionExtensions
{
    public static IServiceCollection AddFrontendLinks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<FrontendOptions>()
            .Bind(configuration.GetSection("Frontend"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<FrontendOptions>, FrontendOptionsValidator>();
        services.AddSingleton<IFrontendUrlBuilder, FrontendUrlBuilder>();

        return services;
    }
}

