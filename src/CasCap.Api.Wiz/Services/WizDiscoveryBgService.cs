namespace CasCap.Services;

/// <summary>
/// Background service that periodically discovers Wiz bulbs on the local network
/// and emits <see cref="WizEvent"/> to registered sinks when bulb state changes are detected.
/// </summary>
public sealed class WizDiscoveryBgService(
    ILogger<WizDiscoveryBgService> logger,
    IOptions<WizConfig> wizConfig,
    TimeProvider timeProvider,
    WizClientService wizClientSvc,
    IEnumerable<IEventSink<WizEvent>> eventSinks) : IBgFeature
{
    private readonly Dictionary<string, WizPilotState> _previousStates = [];

    /// <inheritdoc/>
    public string FeatureName => "Wiz";

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{ClassName} starting Wiz bulb discovery background service",
            nameof(WizDiscoveryBgService));

        foreach (var eventSink in eventSinks)
            await eventSink.InitializeAsync(cancellationToken).ConfigureAwait(false);

        var consecutiveFailures = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var bulbs = await wizClientSvc.DiscoverBulbsAsync(cancellationToken).ConfigureAwait(false);
                logger.LogDebug("{ClassName} discovery cycle complete, {BulbCount} bulb(s) on network",
                    nameof(WizDiscoveryBgService), bulbs.Count);
                if (consecutiveFailures > 0)
                {
                    if (logger.IsEnabled(LogLevel.Information))
                        logger.LogInformation("{ClassName} discovery recovered ConsecutiveFailures={ConsecutiveFailures}",
                            nameof(WizDiscoveryBgService), consecutiveFailures);
                    consecutiveFailures = 0;
                }

                foreach (var bulb in bulbs.Values)
                    await ProcessBulbAsync(bulb, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.Log(consecutiveFailures++ == 0 ? LogLevel.Warning : LogLevel.Debug, ex,
                    "{ClassName} discovery cycle failed ConsecutiveFailures={ConsecutiveFailures}",
                    nameof(WizDiscoveryBgService), consecutiveFailures);
            }

            await Task.Delay(wizConfig.Value.DiscoveryPollingDelayMs, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessBulbAsync(WizBulb bulb, CancellationToken cancellationToken)
    {
        var pilot = bulb.PilotState ?? new WizPilotState();
        var key = bulb.IpAddress;
        var isFirstSeen = !_previousStates.TryGetValue(key, out var previous);
        _previousStates[key] = pilot;

        // Compare ignoring Mac and Rssi which fluctuate between responses.
        var changed = isFirstSeen
            || (pilot with { Mac = null, Rssi = null }) != (previous! with { Mac = null, Rssi = null });
        if (!changed)
            return;

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("{ClassName} bulb state changed, State={State}, Dimming={Dimming}, SceneId={SceneId}, Temp={Temp}, R={R}, G={G}, B={B}",
                nameof(WizDiscoveryBgService), pilot.State, pilot.Dimming, pilot.SceneId, pilot.Temp, pilot.R, pilot.G, pilot.B);

        var wizEvent = new WizEvent
        {
            DeviceId = bulb.Mac ?? bulb.IpAddress,
            IpAddress = bulb.IpAddress,
            Mac = bulb.Mac,
            DeviceName = bulb.DeviceName,
            State = pilot.State ?? false,
            Dimming = pilot.Dimming,
            SceneId = pilot.SceneId,
            Temp = pilot.Temp,
            R = pilot.R,
            G = pilot.G,
            B = pilot.B,
            C = pilot.C,
            W = pilot.W,
            Rssi = pilot.Rssi,
            TimestampUtc = timeProvider.GetUtcNow().UtcDateTime,
        };

        var tasks = new List<Task>(eventSinks.Count());
        foreach (var eventSink in eventSinks)
            tasks.Add(eventSink.WriteEvent(wizEvent, cancellationToken));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}
