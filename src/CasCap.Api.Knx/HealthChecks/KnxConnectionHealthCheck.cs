namespace CasCap.HealthChecks;

/// <summary>
/// Health check for the KNX bus connection.
/// <para>
/// Reports <see cref="HealthStatus.Healthy"/> when either:
/// <list type="bullet">
///   <item>the replica is the active leader and the KNX bus connection is online, or</item>
///   <item>the replica is a standby waiting to acquire the distributed lock.</item>
/// </list>
/// Reports <see cref="HealthStatus.Unhealthy"/> only when the replica is the active leader
/// but has lost the bus connection.
/// </para>
/// <see cref="ConnectionActive"/> is only set to <see langword="true"/> by <see cref="Services.KnxMonitorBgService"/>
/// after <see cref="KnxGroupAddressLookupHealthCheck.GroupAddressesLoaded"/> is already <see langword="true"/>,
/// so services only need to gate on this single health check.
/// </summary>
public class KnxConnectionHealthCheck : IHealthCheck
{
    private volatile bool _connectionActive = false;
    private volatile bool _isLeader = false;

    /// <summary>The health check name.</summary>
    public static string Name => "knx_bus_connection";

    /// <summary>
    /// Gets or sets a value indicating whether the KNX bus connection is active.
    /// This property is only set to <see langword="true"/> after group addresses have been loaded.
    /// </summary>
    public bool ConnectionActive
    {
        get => _connectionActive;
        set => _connectionActive = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether this replica has acquired the distributed lock
    /// and is the active leader responsible for bus connectivity.
    /// </summary>
    public bool IsLeader
    {
        get => _isLeader;
        set => _isLeader = value;
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!IsLeader)
            return Task.FromResult(HealthCheckResult.Healthy("KNX standby — waiting for distributed lock."));

        if (ConnectionActive)
            return Task.FromResult(HealthCheckResult.Healthy("KNX bus connection online!"));

        return Task.FromResult(HealthCheckResult.Unhealthy("KNX bus connection offline!"));
    }
}
