using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Exceptions;

namespace Modulith.Shared.Infrastructure.Logging;

public static class SerilogExtensions
{
    public static IHostBuilder UseModulithSerilog(this IHostBuilder builder)
    {
        return builder.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .Enrich.WithExceptionDetails()
                .Destructure.With<PersonalDataDestructuringPolicy>();
        });
    }

    public static LoggerConfiguration AddModulithDefaults(
        this LoggerConfiguration loggerConfiguration)
    {
        return loggerConfiguration
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .Enrich.WithExceptionDetails()
            .Destructure.With<PersonalDataDestructuringPolicy>();
    }
}
