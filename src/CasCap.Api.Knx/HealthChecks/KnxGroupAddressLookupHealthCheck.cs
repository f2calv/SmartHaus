namespace CasCap.HealthChecks;

/// <summary>Health check that reports healthy once KNX group addresses have been loaded.</summary>
public class KnxGroupAddressLookupHealthCheck : IHealthCheck
{
    private volatile bool _connectionActive = false;

    /// <summary>
    /// The health check name.
    /// </summary>
    public static string Name => "knx_bus_groupaddresslookup";

    /// <summary>
    /// Gets or sets a value indicating whether the group addresses have been loaded.
    /// </summary>
    public bool GroupAddressesLoaded
    {
        get => _connectionActive;
        set => _connectionActive = value;
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (GroupAddressesLoaded)
            return Task.FromResult(HealthCheckResult.Healthy("KNX group addresses loaded!"));

        return Task.FromResult(HealthCheckResult.Unhealthy("KNX group addresses not yet loaded!"));
    }
}
