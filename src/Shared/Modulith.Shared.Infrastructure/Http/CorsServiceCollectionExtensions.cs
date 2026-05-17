using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using BrowserCorsOptions = Modulith.Shared.Infrastructure.Http.CorsOptions;

namespace Modulith.Shared.Infrastructure.Http;

public static class CorsServiceCollectionExtensions
{
    public static IServiceCollection AddBrowserCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<BrowserCorsOptions>()
            .Bind(configuration.GetSection("Cors"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<BrowserCorsOptions>, CorsOptionsValidator>();

        var cors = configuration.GetSection("Cors").Get<BrowserCorsOptions>() ?? new BrowserCorsOptions();
        ValidateCorsOptions(cors);

        services.AddCors(opts => opts.AddPolicy(cors.PolicyName, policy => ConfigurePolicy(policy, cors)));

        return services;
    }

    private static void ValidateCorsOptions(BrowserCorsOptions cors)
    {
        var result = new CorsOptionsValidator().Validate(null, cors);
        if (result.Failed)
        {
            throw new OptionsValidationException(nameof(BrowserCorsOptions), typeof(BrowserCorsOptions), result.Failures);
        }
    }

    private static void ConfigurePolicy(CorsPolicyBuilder policy, BrowserCorsOptions cors)
    {
        // The API uses conventional REST verbs and bearer tokens across routes, so the
        // browser policy accepts any method/header from explicitly trusted origins.
        policy.WithOrigins(cors.AllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();

        if (cors.AllowCredentials)
        {
            policy.AllowCredentials();
        }
    }
}
