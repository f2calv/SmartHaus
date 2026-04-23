using CasCap.Common.Diagnostics.HealthChecks;

namespace CasCap.HealthChecks;

/// <summary>
/// Health check that verifies connectivity to the Miele cloud API.
/// </summary>
public class MieleConnectionHealthCheck(
    ILogger<MieleConnectionHealthCheck> logger,
    IOptions<MieleConfig> config,
    IHostEnvironment env,
    IHttpClientFactory httpClientFactory)
    : HttpEndpointCheckBase(
        logger,
        httpClientFactory.CreateClient(nameof(MieleConnectionHealthCheck)),
        config.Value,
        "Miele API",
        env.IsDevelopment()) // skip health check when in development
{
    /// <summary>The health check registration name.</summary>
    public static string Name => "miele_connection";
}
