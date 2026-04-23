using CasCap.Common.Diagnostics.HealthChecks;

namespace CasCap.HealthChecks;

/// <summary>
/// Health check that verifies connectivity to the Sicce aquarium pump API.
/// </summary>
public class SicceConnectionHealthCheck(
    ILogger<SicceConnectionHealthCheck> logger,
    IOptions<SicceConfig> config,
    IHostEnvironment env,
    IHttpClientFactory httpClientFactory)
    : HttpEndpointCheckBase(
        logger,
        httpClientFactory.CreateClient(nameof(SicceConnectionHealthCheck)),
        config.Value,
        "Sicce",
        env.IsDevelopment()) // skip health check when in development
{
    /// <summary>The health check registration name.</summary>
    public static string Name => "sicce_connection";
}
