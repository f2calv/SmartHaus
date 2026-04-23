using CasCap.Common.Diagnostics.HealthChecks;

namespace CasCap.HealthChecks;

/// <summary>
/// Health check that verifies connectivity to the Buderus KM200 gateway.
/// </summary>
public class BuderusKm200ConnectionHealthCheck(
    ILogger<BuderusKm200ConnectionHealthCheck> logger,
    IOptions<BuderusConfig> config,
    IHostEnvironment env,
    IHttpClientFactory httpClientFactory)
    : HttpEndpointCheckBase(
        logger,
        httpClientFactory.CreateClient(nameof(BuderusKm200ConnectionHealthCheck)),
        config.Value,
        "Buderus KM200",
        env.IsDevelopment()) // skip health check when in development
{
    /// <summary>The health check registration name.</summary>
    public static string Name => "buderus_km200_connection";
}
