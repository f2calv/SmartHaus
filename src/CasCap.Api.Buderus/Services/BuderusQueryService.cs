namespace CasCap.Services;

/// <summary>
/// There is no official API, however the <see cref="PeterPuff"/> package provides access to the Buderus KM200 device which then gives access to the heat pump data.
/// </summary>
/// <remarks>
/// Queries are delegated to the keyed <see cref="SinkServiceCollectionExtensions.PrimarySinkKey"/> sink.
/// </remarks>
public class BuderusQueryService(ILogger<BuderusQueryService> logger, [FromKeyedServices(SinkServiceCollectionExtensions.PrimarySinkKey)] IEventSink<BuderusEvent> primarySink, BuderusKm200ClientService km200ClientSvc, IBuderusQuery? buderusQuery = null) : IBuderusQueryService
{
    /// <summary>
    /// Retrieves events from Redis, optionally filtered by sensor <paramref name="id"/>.
    /// When <paramref name="id"/> is <see langword="null"/>, returns snapshot values;
    /// otherwise returns line-item events for the specified sensor.
    /// </summary>
    /// <param name="id">The Buderus sensor identifier (e.g. <c>_heatingCircuits_hc2_supplyTemperatureSetpoint</c>), or <see langword="null"/> for all snapshot values.</param>
    /// <param name="limit">Maximum number of events to return. Defaults to <c>1000</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public IAsyncEnumerable<BuderusEvent> GetEvents(
        string? id = null,
        int limit = 1000,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("{ClassName} retrieving events, {SensorId}", nameof(BuderusQueryService), id ?? "all");
        return primarySink.GetEvents(id, limit, cancellationToken);
    }

    /// <summary>
    /// Retrieves a typed snapshot of key Buderus heat pump sensor values from Redis.
    /// </summary>
    public async Task<BuderusSnapshot> GetSnapshot()
    {
        logger.LogDebug("{ClassName} retrieving heatpump snapshot", nameof(BuderusQueryService));
        if (buderusQuery is null)
            return new();
        return await buderusQuery.GetSnapshot();
    }

    /// <summary>
    /// Writes a new value to a writeable KM200 datapoint directly on the device.
    /// </summary>
    /// <param name="datapointId">
    /// The full KM200 datapoint path, e.g. <c>/dhwCircuits/dhw1/setTemperature</c> or
    /// <c>/heatingCircuits/hc2/temperatureLevels/normal</c>.
    /// </param>
    /// <param name="value">
    /// The new value to write. For numeric datapoints (type <c>floatValue</c>) supply a decimal number string
    /// (e.g. <c>"55.0"</c>). For enumeration datapoints (type <c>stringValue</c>) supply the option string
    /// (e.g. <c>"Always_On"</c>).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if the device accepted the write; <see langword="false"/> otherwise.</returns>
    public async Task<bool> SetDataPoint(
        string datapointId,
        string value,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("{ClassName} writing {DatapointId}={Value}", nameof(BuderusQueryService), datapointId, value);
        return await km200ClientSvc.SetDataPoint(datapointId, value, cancellationToken);
    }
}
