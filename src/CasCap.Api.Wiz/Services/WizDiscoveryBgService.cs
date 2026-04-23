namespace CasCap.Services;

/// <summary>
/// Background service that periodically discovers Wiz bulbs on the local network
/// and emits <see cref="WizEvent"/> to registered sinks when bulb state changes are detected.
/// </summary>
public class WizDiscoveryBgService(
    ILogger<WizDiscoveryBgService> logger,
    IOptions<WizConfig> wizConfig,
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
            await eventSink.InitializeAsync(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var bulbs = await wizClientSvc.DiscoverBulbsAsync(cancellationToken);
                logger.LogDebug("{ClassName} discovery cycle complete, {BulbCount} bulb(s) on network",
                    nameof(WizDiscoveryBgService), bulbs.Count);

                foreach (var bulb in bulbs.Values)
                {
                    var pilot = bulb.PilotState ?? new WizPilotState();
                    var key = bulb.IpAddress;
                    var isFirstSeen = !_previousStates.TryGetValue(key, out var previous);
                    _previousStates[key] = pilot;

                    // Compare ignoring Mac and Rssi which fluctuate between responses
                    var changed = isFirstSeen
                        || (pilot with { Mac = null, Rssi = null }) != (previous! with { Mac = null, Rssi = null });

                    if (changed)
                    {
                        logger.LogInformation("{ClassName} bulb {DeviceName} ({IpAddress}) state changed, State={State}, Dimming={Dimming}, SceneId={SceneId}, Temp={Temp}, R={R}, G={G}, B={B}",
                            nameof(WizDiscoveryBgService), bulb.DeviceName ?? bulb.IpAddress, bulb.IpAddress, pilot.State, pilot.Dimming, pilot.SceneId, pilot.Temp, pilot.R, pilot.G, pilot.B);

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
                            TimestampUtc = DateTime.UtcNow,
                        };

                        var tasks = new List<Task>(eventSinks.Count());
                        foreach (var eventSink in eventSinks)
                            tasks.Add(eventSink.WriteEvent(wizEvent, cancellationToken));
                        await Task.WhenAll(tasks);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "{ClassName} discovery cycle failed", nameof(WizDiscoveryBgService));
            }

            await Task.Delay(wizConfig.Value.DiscoveryPollingDelayMs, cancellationToken);
        }
    }
}
