using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Reflection;

namespace AzmCrm.API.Controllers.Base;

[AllowAnonymous]
[Route("health")]
public sealed class HealthController(
    HealthCheckService healthCheckService,
    ILogger<HealthController> logger) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetHealth()
    {
        var healthReport = await healthCheckService.CheckHealthAsync();

        var response = new HealthCheckResponse
        {
            Status = healthReport.Status.ToString(),
            Timestamp = DateTime.UtcNow,
            Version = GetApplicationVersion(),
            Checks = healthReport.Entries.ToDictionary(
                entry => entry.Key,
                entry => new HealthCheckDetail
                {
                    Status = entry.Value.Status.ToString(),
                    Description = entry.Value.Description,
                    Duration = entry.Value.Duration.TotalMilliseconds
                }
            )
        };

        // This endpoint backs the deployment's readiness/liveness probe, so it must only fail
        // when this instance itself can't serve traffic. Checks tagged "external" report status
        // in the response body for visibility but never block the pod from becoming Ready.
        var isReady = healthReport.Entries.Values
            .Where(entry => !entry.Tags.Contains("external"))
            .All(entry => entry.Status == HealthStatus.Healthy);

        logger.LogInformation("Health check requested - Status: {Status}", response.Status);

        return isReady
            ? Ok(response)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }

    private static string GetApplicationVersion()
    {
        var envVersion = Environment.GetEnvironmentVariable("APP_VERSION");
        if (!string.IsNullOrEmpty(envVersion))
        {
            return envVersion.Length > 7 ? envVersion[..7] : envVersion;
        }

        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version?.ToString() ?? "1.0.0";
    }
}

public sealed record HealthCheckResponse
{
    public required string Status { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string Version { get; init; }
    public Dictionary<string, HealthCheckDetail> Checks { get; init; } = [];
}

public sealed record HealthCheckDetail
{
    public required string Status { get; init; }
    public string? Description { get; init; }
    public double Duration { get; init; }
}
