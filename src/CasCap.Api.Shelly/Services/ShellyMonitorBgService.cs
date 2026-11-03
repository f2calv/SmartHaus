namespace CasCap.Services;

/// <summary>Background service for polling all configured Shelly smart plugs and publishing events to sinks.</summary>
public sealed partial class ShellyMonitorBgService(
    ILogger<ShellyMonitorBgService> logger,
    IOptions<ShellyConfig> shellyConfig,
    ShellyCloudConnectionHealthCheck shellyCloudConnectionHealthCheck,
    ShellyCloudClientService shellyCloudClientSvc,
    IHostEnvironment env,
    TimeProvider timeProvider,
    IEnumerable<IEventSink<ShellyEvent>> eventSinks
    ) : IBgFeature
{
    /// <inheritdoc/>
    public string FeatureName => "Shelly";

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (eventSinks.IsNullOrEmpty()) throw new ConfigurationException($"no {nameof(IEventSink<ShellyEvent>)} is configured!");
        logger.LogInformation("{ClassName} starting, {DeviceCount} device(s) configured",
            nameof(ShellyMonitorBgService), shellyConfig.Value.Devices.Length);
        try
        {
            await ValidateConfiguredDevicesAsync().ConfigureAwait(false);
            await RunServiceAsync(cancellationToken).ConfigureAwait(false);
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
            await Task.Delay(1_500).ConfigureAwait(false);

            var response = await shellyCloudClientSvc.GetDeviceStatus(device.DeviceId).ConfigureAwait(false);
            if (response is null || !response.IsOk)
                OnDeviceFailed(device);
            else if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("{ClassName} validated device {DeviceId} ({DeviceName}) exists in Shelly Cloud account",
                    nameof(ShellyMonitorBgService), device.DeviceId, device.DeviceName);
        }
    }

    private async Task RunServiceAsync(CancellationToken cancellationToken)
    {
        foreach (var eventSink in eventSinks)
            await eventSink.InitializeAsync(cancellationToken).ConfigureAwait(false);

        var attempt = 1;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (shellyCloudConnectionHealthCheck.ConnectionActive || env.IsDevelopment())
            {
                await RecordDataPoints().ConfigureAwait(false);
                await Task.Delay(shellyConfig.Value.PollingIntervalMs, cancellationToken).ConfigureAwait(false);
                attempt = 1;
            }
            else
            {
                logger.Log(attempt % shellyConfig.Value.ConnectionLogEscalationInterval == 0 ? LogLevel.Warning : LogLevel.Trace,
                    "{ClassName} readiness probe not yet healthy, attempt {Attempt}, retry in {RetryMs}ms...",
                    nameof(ShellyMonitorBgService), attempt, shellyConfig.Value.ConnectionPollingDelayMs);
                await Task.Delay(shellyConfig.Value.ConnectionPollingDelayMs, cancellationToken).ConfigureAwait(false);
                attempt++;
            }
        }

        async Task RecordDataPoints()
        {
            foreach (var device in shellyConfig.Value.Devices)
            {
                // Shelly Cloud API is rate-limited to 1 req/sec — throttle before every call
                await Task.Delay(1_500, cancellationToken).ConfigureAwait(false);

                var response = await shellyCloudClientSvc.GetDeviceStatus(device.DeviceId).ConfigureAwait(false);
                if (response is not null && response.IsOk)
                {
                    var shellyEvent = new ShellyEvent(device, response, timeProvider.GetUtcNow().UtcDateTime);

                    var tasks = new List<Task>(eventSinks.Count());
                    foreach (var eventSink in eventSinks)
                        tasks.Add(eventSink.WriteEvent(shellyEvent, cancellationToken));
                    await Task.WhenAll(tasks.ToArray()).ConfigureAwait(false);
                    OnDeviceSucceeded(device);
                }
                else
                    OnDeviceFailed(device);
            }
        }
    }

    private readonly Dictionary<string, (int ConsecutiveFailures, DateTimeOffset FailingSince)> _failingDevices = [];

    private void OnDeviceFailed(ShellyDevice device)
    {
        if (_failingDevices.TryGetValue(device.DeviceId, out var failure))
        {
            _failingDevices[device.DeviceId] = (failure.ConsecutiveFailures + 1, failure.FailingSince);
            LogDeviceStillFailing(logger, nameof(ShellyMonitorBgService), device.DeviceId, failure.ConsecutiveFailures + 1);
            return;
        }
        _failingDevices[device.DeviceId] = (1, timeProvider.GetUtcNow());
        LogDeviceFailing(logger, nameof(ShellyMonitorBgService), device.DeviceId, device.DeviceName);
    }

    private void OnDeviceSucceeded(ShellyDevice device)
    {
        if (!_failingDevices.Remove(device.DeviceId, out var failure))
            return;
        LogDeviceRecovered(logger, nameof(ShellyMonitorBgService), device.DeviceId, device.DeviceName,
            failure.ConsecutiveFailures, timeProvider.GetUtcNow() - failure.FailingSince);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{ClassName} device {DeviceId} ({DeviceName}) not reachable via Shelly Cloud, device may be unplugged or offline")]
    private static partial void LogDeviceFailing(ILogger logger, string className, string deviceId, string deviceName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{ClassName} device {DeviceId} still not reachable ConsecutiveFailures={ConsecutiveFailures}")]
    private static partial void LogDeviceStillFailing(ILogger logger, string className, string deviceId, int consecutiveFailures);

    [LoggerMessage(Level = LogLevel.Information, Message = "{ClassName} device {DeviceId} ({DeviceName}) recovered ConsecutiveFailures={ConsecutiveFailures} Downtime={Downtime}")]
    private static partial void LogDeviceRecovered(ILogger logger, string className, string deviceId, string deviceName, int consecutiveFailures, TimeSpan downtime);
}
