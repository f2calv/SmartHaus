namespace CasCap.HealthChecks;

/// <summary>
/// Health check that verifies connectivity to the signal-cli REST API.
/// </summary>
public class SignalCliConnectionHealthCheck(
    ILogger<SignalCliConnectionHealthCheck> logger,
    IOptions<SignalCliConfig> config,
    IHostEnvironment env,
    IHttpClientFactory httpClientFactory)
    : HttpEndpointCheckBase(
        logger,
        httpClientFactory.CreateClient(nameof(SignalCliConnectionHealthCheck)),
        config.Value,
        "signal-cli REST API",
        env.IsDevelopment()) // skip health check when in development
{
    /// <summary>The health check registration name.</summary>
    public static string Name => "signal_cli_connection";
}
