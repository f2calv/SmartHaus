namespace CasCap.Services;

/// <summary>
/// Manipulates endpoint+event data from the Buderus KM200 in conjunction with EventSinks.
/// </summary>
public sealed class BuderusKm200MonitorBgService(
    ILogger<BuderusKm200MonitorBgService> logger,
    IOptions<BuderusConfig> buderusConfig,
    TimeProvider timeProvider,
    BuderusKm200ConnectionHealthCheck km200ConnectionHealthCheck,
    BuderusKm200ClientService buderusKm200ClientSvc,
    IEnumerable<IEventSink<BuderusEvent>> eventSinks
    ) : IBgFeature
{

    /// <inheritdoc/>
    public string FeatureName => "Buderus";

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (eventSinks.IsNullOrEmpty()) throw new GenericException($"no {nameof(IEventSink<BuderusEvent>)} is configured!");
        logger.LogInformation("{ClassName} starting", nameof(BuderusKm200MonitorBgService));
        try
        {
            logger.LogInformation("{ClassName} endpoint is '{Endpoint}'", nameof(BuderusKm200MonitorBgService), buderusKm200ClientSvc.Client.BaseAddress);
            await RunServiceAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException) { throw; }
        logger.LogInformation("{ClassName} exiting", nameof(BuderusKm200MonitorBgService));
    }

    private Task RunServiceAsync(CancellationToken cancellationToken) => RunLogger(cancellationToken);

    /// <summary>Polls configured datapoints and writes events to sinks.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RunLogger(CancellationToken cancellationToken)
    {
        if (buderusConfig.Value.DatapointMappings.Count == 0)
        {
            logger.LogWarning("{ClassName} no {Property} configured, polling disabled",
                nameof(BuderusKm200MonitorBgService), nameof(BuderusConfig.DatapointMappings));
            return;
        }

        logger.LogInformation("{ClassName} polling {Count} configured datapoints",
            nameof(BuderusKm200MonitorBgService), buderusConfig.Value.DatapointMappings.Count);
        foreach (var (id, column) in buderusConfig.Value.DatapointMappings)
            logger.LogInformation("{ClassName} configured datapoint {DatapointId} -> {ColumnName}", nameof(BuderusKm200MonitorBgService), id, column);

        foreach (var eventSink in eventSinks)
            await eventSink.InitializeAsync(cancellationToken).ConfigureAwait(false);

        var attempt = 1;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (km200ConnectionHealthCheck.ConnectionActive)
            {
                await RecordDataPoints().ConfigureAwait(false);
                await Task.Delay(buderusConfig.Value.PollingIntervalMs, cancellationToken).ConfigureAwait(false);
                attempt = 1;
            }
            else
            {
                logger.Log(attempt % buderusConfig.Value.ConnectionLogEscalationInterval == 0 ? LogLevel.Warning : LogLevel.Trace,
                    "{ClassName} readiness probe not yet healthy, attempt {Attempt}, retry in {RetryMs}ms...",
                    nameof(BuderusKm200MonitorBgService), attempt, buderusConfig.Value.ConnectionPollingDelayMs);
                await Task.Delay(buderusConfig.Value.ConnectionPollingDelayMs, cancellationToken).ConfigureAwait(false);
                attempt++;
            }
        }

        async Task RecordDataPoints()
        {
            foreach (var datapointId in buderusConfig.Value.DatapointMappings.Keys)
            {
                var dp = await buderusKm200ClientSvc.GetDataPoint(datapointId).ConfigureAwait(false);
                if (dp is not null && dp.Value is not null)
                {
                    var buderusEvent = new BuderusEvent(datapointId, dp.Value, timeProvider.GetUtcNow().UtcDateTime);

                    var tasks = new List<Task>(eventSinks.Count());
                    foreach (var eventSink in eventSinks)
                        tasks.Add(eventSink.WriteEvent(buderusEvent, cancellationToken));
                    await Task.WhenAll(tasks.ToArray()).ConfigureAwait(false);
                    logger.LogDebug("{ClassName} logged datapoint {@BuderusEvent}", nameof(BuderusKm200MonitorBgService), buderusEvent);
                }
                else
                {
                    logger.LogWarning("{ClassName} datapoint id {Id} returns a null object", nameof(BuderusKm200MonitorBgService), datapointId);
                }
                //TODO: investigate how low DatapointDelayMs can be reduced without overwhelming the KM200 gateway
                await Task.Delay(buderusConfig.Value.DatapointDelayMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }

}
