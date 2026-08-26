using Serilog;
using Serilog.Events;

namespace AzmCrm.API.Extensions;

public static class SerilogExtensions
{
    public static Serilog.ILogger CreateLogger()
    {
        return new LoggerConfiguration()
            .ReadFrom.Configuration(new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
                .Build())
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "AzmCrm")
            .CreateLogger();
    }

    public static IApplicationBuilder UseCustomSerilogRequestLogging(this IApplicationBuilder app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
                diagnosticContext.Set("RemoteIP", httpContext.Connection.RemoteIpAddress?.ToString());
            };
            options.GetLevel = (httpContext, elapsed, ex) => LogEventLevel.Information;
        });

        return app;
    }
}
