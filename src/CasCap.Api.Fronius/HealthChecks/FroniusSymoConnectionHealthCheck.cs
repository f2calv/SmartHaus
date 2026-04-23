using CasCap.Common.Diagnostics.HealthChecks;

namespace CasCap.HealthChecks;

/// <summary>
/// Health check that verifies connectivity to the Fronius Symo solar inverter.
/// </summary>
public class FroniusSymoConnectionHealthCheck(
    ILogger<FroniusSymoConnectionHealthCheck> logger,
    IOptions<FroniusConfig> config,
    IHostEnvironment env,
    IHttpClientFactory httpClientFactory)
    : HttpEndpointCheckBase(
        logger,
        httpClientFactory.CreateClient(nameof(FroniusSymoConnectionHealthCheck)),
        config.Value,
        "Inverter",
        env.IsDevelopment()) // skip health check when in development
{
    /// <summary>The health check registration name.</summary>
    public static string Name => "fronius_symo_connection";
}
