namespace CasCap.Services;

/// <summary>Background service for polling the Fronius Symo inverter and publishing events to sinks.</summary>
public class FroniusMonitorBgService(
    ILogger<FroniusMonitorBgService> logger,
    IOptions<FroniusConfig> froniusConfig,
    FroniusSymoConnectionHealthCheck froniusSymoConnectionHealthCheck,
    FroniusClientService froniusClientSvc,
    IEnumerable<IEventSink<FroniusEvent>> eventSinks
    ) : IBgFeature
{

    /// <inheritdoc/>
    public string FeatureName => "Fronius";

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (eventSinks.IsNullOrEmpty()) throw new GenericException($"no {nameof(IEventSink<FroniusEvent>)} is configured!");
        logger.LogInformation("{ClassName} starting", nameof(FroniusMonitorBgService));
        try
        {
            await RunServiceAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException) { throw; }
        logger.LogInformation("{ClassName} exiting", nameof(FroniusMonitorBgService));
    }

    private async Task RunServiceAsync(CancellationToken cancellationToken)
    {
        foreach (var eventSink in eventSinks)
            await eventSink.InitializeAsync(cancellationToken);

        var attempt = 1;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (froniusSymoConnectionHealthCheck.ConnectionActive)
            {
                await RecordDataPoints();
                await Task.Delay(froniusConfig.Value.PollingIntervalMs, cancellationToken);
                attempt = 1;
            }
            else
            {
                logger.Log(attempt % froniusConfig.Value.ConnectionLogEscalationInterval == 0 ? LogLevel.Warning : LogLevel.Trace,
                    "{ClassName} readiness probe not yet healthy, attempt {Attempt}, retry in {RetryMs}ms...",
                    nameof(FroniusMonitorBgService), attempt, froniusConfig.Value.ConnectionPollingDelayMs);
                await Task.Delay(froniusConfig.Value.ConnectionPollingDelayMs, cancellationToken);
                attempt++;
            }
        }

        async Task RecordDataPoints()
        {
            var response = await froniusClientSvc.GetPowerFlowRealtimeData();
            if (response is not null)
            {
                var froniusEvent = new FroniusEvent(response);
                logger.LogTrace("{ClassName} logged datapoint {@Data}", nameof(FroniusMonitorBgService), froniusEvent);

                var tasks = new List<Task>(eventSinks.Count());
                foreach (var eventSink in eventSinks)
                    tasks.Add(eventSink.WriteEvent(froniusEvent, cancellationToken));
                await Task.WhenAll(tasks.ToArray());
            }
            else
                logger.LogWarning("{ClassName} get solar readings returns a null object", nameof(FroniusMonitorBgService));
        }
    }
}
