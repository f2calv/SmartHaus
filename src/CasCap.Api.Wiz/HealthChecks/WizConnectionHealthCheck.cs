using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CasCap.HealthChecks;

/// <summary>
/// Health check that verifies at least one Wiz bulb is reachable on the local network
/// by checking the count of discovered bulbs.
/// </summary>
public class WizConnectionHealthCheck(
    ILogger<WizConnectionHealthCheck> logger,
    WizClientService wizClientSvc) : IHealthCheck
{
    /// <summary>The health check registration name.</summary>
    public static string Name => "wiz_connection";

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var bulbCount = wizClientSvc.DiscoveredBulbs.Count;
        if (bulbCount > 0)
        {
            logger.LogDebug("{ClassName} healthy, {BulbCount} bulb(s) discovered",
                nameof(WizConnectionHealthCheck), bulbCount);
            return Task.FromResult(HealthCheckResult.Healthy($"{bulbCount} Wiz bulb(s) discovered"));
        }

        logger.LogWarning("{ClassName} degraded, no bulbs discovered", nameof(WizConnectionHealthCheck));
        return Task.FromResult(HealthCheckResult.Degraded("No Wiz bulbs discovered on the network"));
    }
}
