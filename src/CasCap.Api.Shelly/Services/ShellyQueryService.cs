namespace CasCap.Services;

/// <summary>
/// Provides access to Shelly smart plug data by querying the primary sink or the Cloud API directly.
/// </summary>
/// <remarks>
/// Key functions are also made accessible via <see cref="Controllers.ShellyController"/>.
/// Queries are delegated to the keyed <see cref="SinkServiceCollectionExtensions.PrimarySinkKey"/> sink.
/// </remarks>
public class ShellyQueryService(
    ILogger<ShellyQueryService> logger,
    IOptions<ShellyConfig> config,
    ShellyCloudClientService clientSvc,
    [FromKeyedServices(SinkServiceCollectionExtensions.PrimarySinkKey)] IEventSink<ShellyEvent> primarySink,
    IShellyQuery? shellyQuery = null) : IShellyQueryService
{
    /// <summary>
    /// Retrieves the current device status from the Shelly Cloud API.
    /// </summary>
    /// <param name="deviceId">The Shelly device ID to query.</param>
    public async Task<ShellyDeviceStatusResponse?> GetDeviceStatus(string deviceId)
    {
        logger.LogDebug("{ClassName} retrieving device status for {DeviceId}", nameof(ShellyQueryService), deviceId);
        return await clientSvc.GetDeviceStatus(deviceId);
    }

    /// <summary>
    /// Controls the relay state (on/off) via the Shelly Cloud API.
    /// </summary>
    /// <param name="deviceId">The Shelly device ID to control.</param>
    /// <param name="turnOn">
    /// When <see langword="true"/>, turns the relay on; when <see langword="false"/>, turns the relay off.
    /// </param>
    public async Task<ShellyRelayControlResponse?> SetRelayState(string deviceId, bool turnOn)
    {
        var device = config.Value.Devices.FirstOrDefault(d => d.DeviceId == deviceId);
        if (device is null)
        {
            logger.LogWarning("{ClassName} device {DeviceId} not found in configuration", nameof(ShellyQueryService), deviceId);
            return null;
        }
        logger.LogInformation("{ClassName} setting relay state to {DesiredState} for {DeviceId} ({DeviceName})",
            nameof(ShellyQueryService), turnOn ? "on" : "off", deviceId, device.DeviceName);
        return await clientSvc.SetRelayState(deviceId, device.Channel, turnOn);
    }

    /// <summary>
    /// Retrieves snapshots for all known smart plug devices from the primary sink.
    /// </summary>
    public async Task<List<ShellySnapshot>> GetSnapshots()
    {
        if (shellyQuery is null)
            return [];
        return await shellyQuery.GetSnapshots();
    }

    /// <summary>
    /// Retrieves smart plug line item readings for the current day.
    /// </summary>
    /// <param name="limit">Maximum number of records to return. Default 100, maximum 1000.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public IAsyncEnumerable<ShellyEvent> GetReadings(
        int limit = 100,
        CancellationToken cancellationToken = default)
        => primarySink.GetEvents(limit: limit, cancellationToken: cancellationToken);
}
