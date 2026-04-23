namespace CasCap.Services;

/// <summary>Background service for polling all configured Shelly smart plugs and publishing events to sinks.</summary>
public class ShellyMonitorBgService(
    ILogger<ShellyMonitorBgService> logger,
    IOptions<ShellyConfig> shellyConfig,
    ShellyCloudConnectionHealthCheck shellyCloudConnectionHealthCheck,
    ShellyCloudClientService shellyCloudClientSvc,
    IHostEnvironment env,
    IEnumerable<IEventSink<ShellyEvent>> eventSinks
    ) : IBgFeature
{
    /// <inheritdoc/>
    public string FeatureName => "Shelly";

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (eventSinks.IsNullOrEmpty()) throw new GenericException($"no {nameof(IEventSink<ShellyEvent>)} is configured!");
        logger.LogInformation("{ClassName} starting, {DeviceCount} device(s) configured",
            nameof(ShellyMonitorBgService), shellyConfig.Value.Devices.Length);
        try
        {
            await ValidateConfiguredDevicesAsync();
            await RunServiceAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException) { throw; }
        logger.LogInformation("{ClassName} exiting", nameof(ShellyMonitorBgService));
    }

    /// <summary>
    /// Validates each configured device at startup by calling <c>GetDeviceStatus</c>.
    /// Unreachable devices are logged as warnings but do not prevent the service from starting.
    /// </summary>
    private async Task ValidateConfiguredDevicesAsync()
    {
        foreach (var device in shellyConfig.Value.Devices)
        {
            // Shelly Cloud API is rate-limited to 1 req/sec — throttle before every call
            await Task.Delay(1_500);

            var response = await shellyCloudClientSvc.GetDeviceStatus(device.DeviceId);
            if (response is null || !response.IsOk)
                logger.LogWarning("{ClassName} configured device {DeviceId} ({DeviceName}) not reachable via Shelly Cloud — device may be unplugged or offline",
                    nameof(ShellyMonitorBgService), device.DeviceId, device.DeviceName);
            else
                logger.LogInformation("{ClassName} validated device {DeviceId} ({DeviceName}) exists in Shelly Cloud account",
                    nameof(ShellyMonitorBgService), device.DeviceId, device.DeviceName);
        }
    }

    private async Task RunServiceAsync(CancellationToken cancellationToken)
    {
        foreach (var eventSink in eventSinks)
            await eventSink.InitializeAsync(cancellationToken);

        var attempt = 1;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (shellyCloudConnectionHealthCheck.ConnectionActive || env.IsDevelopment())
            {
                await RecordDataPoints();
                await Task.Delay(shellyConfig.Value.PollingIntervalMs, cancellationToken);
                attempt = 1;
            }
            else
            {
                logger.Log(attempt % shellyConfig.Value.ConnectionLogEscalationInterval == 0 ? LogLevel.Warning : LogLevel.Trace,
                    "{ClassName} readiness probe not yet healthy, attempt {Attempt}, retry in {RetryMs}ms...",
                    nameof(ShellyMonitorBgService), attempt, shellyConfig.Value.ConnectionPollingDelayMs);
                await Task.Delay(shellyConfig.Value.ConnectionPollingDelayMs, cancellationToken);
                attempt++;
            }
        }

        async Task RecordDataPoints()
        {
            foreach (var device in shellyConfig.Value.Devices)
            {
                // Shelly Cloud API is rate-limited to 1 req/sec — throttle before every call
                await Task.Delay(1_500, cancellationToken);

                var response = await shellyCloudClientSvc.GetDeviceStatus(device.DeviceId);
                if (response is not null && response.IsOk)
                {
                    var shellyEvent = new ShellyEvent(device, response);

                    var tasks = new List<Task>(eventSinks.Count());
                    foreach (var eventSink in eventSinks)
                        tasks.Add(eventSink.WriteEvent(shellyEvent, cancellationToken));
                    await Task.WhenAll(tasks.ToArray());
                }
                else
                    logger.LogWarning("{ClassName} get device status returns a null or failed response for {DeviceId} ({DeviceName})",
                        nameof(ShellyMonitorBgService), device.DeviceId, device.DeviceName);
            }
        }
    }
}
