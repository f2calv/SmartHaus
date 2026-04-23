using CasCap.Common.Diagnostics.HealthChecks;

namespace CasCap.HealthChecks;

/// <summary>
/// Health check that verifies connectivity to the DoorBird door entry system.
/// </summary>
public class DoorBirdConnectionHealthCheck(
    ILogger<DoorBirdConnectionHealthCheck> logger,
    IOptions<DoorBirdConfig> config,
    IHostEnvironment env,
    IHttpClientFactory httpClientFactory)
    : HttpEndpointCheckBase(
        logger,
        httpClientFactory.CreateClient(nameof(DoorBirdConnectionHealthCheck)),
        config.Value,
        "DoorBird",
        env.IsDevelopment()) // skip health check when in development
{
    /// <summary>The health check registration name.</summary>
    public static string Name => "doorbird_connection";
}
