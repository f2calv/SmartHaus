namespace CasCap.Abstractions;

/// <summary>
/// Defines the public query and control operations exposed by the Shelly smart plug service.
/// </summary>
public interface IShellyQueryService
{
    /// <summary>
    /// Retrieves the current device status from the Shelly Cloud API.
    /// </summary>
    /// <param name="deviceId">The Shelly device ID to query.</param>
    Task<ShellyDeviceStatusResponse?> GetDeviceStatus(string deviceId);

    /// <summary>
    /// Controls the relay state (on/off) via the Shelly Cloud API.
    /// </summary>
    /// <param name="deviceId">The Shelly device ID to control.</param>
    /// <param name="turnOn">
    /// When <see langword="true"/>, turns the relay on; when <see langword="false"/>, turns the relay off.
    /// </param>
    Task<ShellyRelayControlResponse?> SetRelayState(string deviceId, bool turnOn);

    /// <summary>
    /// Retrieves snapshots for all known smart plug devices from the primary sink.
    /// </summary>
    Task<List<ShellySnapshot>> GetSnapshots();

    /// <summary>
    /// Retrieves smart plug line item readings for the current day.
    /// </summary>
    /// <param name="limit">Maximum number of records to return. Default 100, maximum 1000.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<ShellyEvent> GetReadings(int limit = 100, CancellationToken cancellationToken = default);
}
