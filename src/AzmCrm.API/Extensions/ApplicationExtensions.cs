using AzmCrm.API.Settings;
using AzmCrm.Application;
using AzmCrm.Domain;
using AzmCrm.Infrastructure;

namespace AzmCrm.API.Extensions;

public static class ApplicationExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddControllers();

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
