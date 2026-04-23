namespace CasCap.Abstractions;

/// <summary>
/// Defines the public query and control operations exposed by the Buderus KM200 heat pump service.
/// </summary>
public interface IBuderusQueryService
{
    /// <summary>
    /// Retrieves events from the primary sink, optionally filtered by sensor <paramref name="id"/>.
    /// When <paramref name="id"/> is <see langword="null"/>, returns snapshot values;
    /// otherwise returns line-item events for the specified sensor.
    /// </summary>
    /// <param name="id">The Buderus sensor identifier (e.g. <c>_heatingCircuits_hc2_supplyTemperatureSetpoint</c>), or <see langword="null"/> for all snapshot values.</param>
    /// <param name="limit">Maximum number of events to return. Defaults to <c>1000</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<BuderusEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a typed snapshot of key Buderus heat pump sensor values from the primary sink.
    /// </summary>
    Task<BuderusSnapshot> GetSnapshot();

    /// <summary>
    /// Writes a new value to a writeable KM200 datapoint directly on the device.
    /// </summary>
    /// <param name="datapointId">The full KM200 datapoint path, e.g. <c>/dhwCircuits/dhw1/setTemperature</c>.</param>
    /// <param name="value">The new value to write. For numeric datapoints supply a decimal string (e.g. <c>"55.0"</c>); for enumeration datapoints supply the option string (e.g. <c>"Always_On"</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if the device accepted the write; <see langword="false"/> otherwise.</returns>
    Task<bool> SetDataPoint(string datapointId, string value, CancellationToken cancellationToken = default);
}
