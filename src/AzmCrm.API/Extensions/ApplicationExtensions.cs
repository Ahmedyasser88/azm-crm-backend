using AzmCrm.API.Settings;
using AzmCrm.Application;
using AzmCrm.Domain;
using AzmCrm.Infrastructure;
using System.Text.Json.Serialization;

namespace AzmCrm.API.Extensions;

public static class ApplicationExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddControllers()
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        // SignalR's hub protocol has its own JSON serializer, separate from MVC's — without this,
        // enums (e.g. MessageDto.Direction) serialize as integers over the hub while every REST
        // response serializes them as strings via the JsonStringEnumConverter above.
        services.AddSignalR()
            .AddJsonProtocol(options =>
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddDomain();
        services.AddApplication();
        services.AddInfrastructure(configuration);

        services.AddHealthChecks()
            .AddNpgSql(
                configuration.GetConnectionString("DefaultConnection")!,
                name: "database",
                tags: ["db", "postgresql"]);

        return services;
    }

    public static IServiceCollection AddCustomCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<CorsSettings>(configuration.GetSection(CorsSettings.SectionName));

        var allowedOrigins = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>()?.AllowedOrigins
                              ?? [];

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.SetIsOriginAllowed(origin => CorsOriginValidator.IsAllowed(origin, allowedOrigins))
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        return services;
    }
}
