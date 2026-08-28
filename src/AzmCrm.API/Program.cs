using AzmCrm.API.Extensions;
using AzmCrm.API.Hubs;
using AzmCrm.API.Middleware;
using AzmCrm.Infrastructure.Data;
using Serilog;

Log.Logger = SerilogExtensions.CreateLogger();

try
{
    Log.Information("Starting AzmCrm API");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    // Bound how long the host waits during a graceful shutdown (SIGTERM).
    // Kubernetes keeps a rolling deployment's old Pod around ("1 old replicas
    // are pending termination") for as long as this process takes to exit.
    // An unbounded/blocked shutdown (a hung request, a hosted service that
    // never observes cancellation, etc.) directly extends how long
    // `kubectl rollout status` has to wait, which is what caused the pipeline
    // to time out. Fifteen seconds is comfortably inside the default
    // terminationGracePeriodSeconds (30s) so the old Pod is always gone fast.
    builder.Host.ConfigureHostOptions(options =>
    {
        options.ShutdownTimeout = TimeSpan.FromSeconds(15);
    });

    // Allow file uploads up to 50 MB
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = 52_428_800; // 50 MB
    });

    builder.Services.AddApplicationServices(builder.Configuration);
    builder.Services.AddCustomCors(builder.Configuration);
    builder.Services.AddCustomSwagger();
    builder.Services.AddCustomRateLimiting(builder.Configuration);

    var app = builder.Build();

    await InitializeDatabaseAsync(app);

    // ── CORS must be the very first middleware so that preflight OPTIONS
    //    requests are answered immediately and every error response
    //    (rate-limit 429, auth 401, unhandled 500, …) still carries
    //    the required CORS headers.  Without this, the browser masks
    //    the real status code behind a generic "CORS error".
    app.UseCors();

    app.UseMiddleware<LocalizationMiddleware>();
    app.UseCustomSerilogRequestLogging();

    app.UseRateLimiter();
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    app.UseCustomSwagger(app.Environment);

    app.UseHttpsRedirection();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHub<ChatHub>("/hubs/chat");

    Log.Information("AzmCrm API started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static async Task InitializeDatabaseAsync(WebApplication app)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
        await initializer.InitializeAsync();
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error during database initialization - {ErrorMessage}", ex.Message);
    }
}
