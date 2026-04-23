using CasCap.Common.Diagnostics.HealthChecks;

namespace CasCap.HealthChecks;

/// <summary>
/// Health check that verifies connectivity to the Shelly Cloud API.
/// </summary>
public class ShellyCloudConnectionHealthCheck(
    ILogger<ShellyCloudConnectionHealthCheck> logger,
    IOptions<ShellyConfig> config,
    IHostEnvironment env,
    IHttpClientFactory httpClientFactory)
    : HttpEndpointCheckBase(
        logger,
        httpClientFactory.CreateClient(nameof(ShellyCloudConnectionHealthCheck)),
        config.Value,
        "SmartPlug",
        env.IsDevelopment()) // skip health check when in development
{
    /// <summary>The health check registration name.</summary>
    public static string Name => "shelly_cloud_connection";
}
