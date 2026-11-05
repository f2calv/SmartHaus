namespace CasCap.Services;

/// <summary>Background service for polling the Fronius Symo inverter and publishing events to sinks.</summary>
public sealed partial class FroniusMonitorBgService(
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
        if (eventSinks.IsNullOrEmpty()) throw new ConfigurationException($"no {nameof(IEventSink<FroniusEvent>)} is configured!");
        logger.LogInformation("{ClassName} starting", nameof(FroniusMonitorBgService));
        try
        {
            await RunServiceAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException) { throw; }
        logger.LogInformation("{ClassName} exiting", nameof(FroniusMonitorBgService));
    }

    private async Task RunServiceAsync(CancellationToken cancellationToken)
    {
        foreach (var eventSink in eventSinks)
            await eventSink.InitializeAsync(cancellationToken).ConfigureAwait(false);

        var attempt = 1;
        var consecutiveFailures = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (froniusSymoConnectionHealthCheck.ConnectionActive)
            {
                if (attempt > 1)
                    LogConnectionRecovered(logger, nameof(FroniusMonitorBgService), attempt);
                consecutiveFailures = await RecordDataPointsAsync(consecutiveFailures, cancellationToken).ConfigureAwait(false);
                await Task.Delay(froniusConfig.Value.PollingIntervalMs, cancellationToken).ConfigureAwait(false);
                attempt = 1;
            }
            else
            {
                logger.Log(attempt % froniusConfig.Value.ConnectionLogEscalationInterval == 0 ? LogLevel.Warning : LogLevel.Trace,
                    "{ClassName} readiness probe not yet healthy, attempt {Attempt}, retry in {RetryMs}ms...",
                    nameof(FroniusMonitorBgService), attempt, froniusConfig.Value.ConnectionPollingDelayMs);
                await Task.Delay(froniusConfig.Value.ConnectionPollingDelayMs, cancellationToken).ConfigureAwait(false);
                attempt++;
            }
        }

    }

    private async Task<int> RecordDataPointsAsync(int consecutiveFailures, CancellationToken cancellationToken)
    {
        var response = await froniusClientSvc.GetPowerFlowRealtimeData().ConfigureAwait(false);
        if (response is not null)
        {
            var froniusEvent = new FroniusEvent(response);
            LogDatapointRecorded(logger, nameof(FroniusMonitorBgService));
            if (consecutiveFailures > 0)
            {
                LogReadingsRecovered(logger, nameof(FroniusMonitorBgService), consecutiveFailures);
                consecutiveFailures = 0;
            }

            var tasks = new List<Task>(eventSinks.Count());
            foreach (var eventSink in eventSinks)
                tasks.Add(eventSink.WriteEvent(froniusEvent, cancellationToken));
            await Task.WhenAll(tasks.ToArray()).ConfigureAwait(false);
        }
        else if (consecutiveFailures++ == 0)
            LogReadingsFailing(logger, nameof(FroniusMonitorBgService));
        else
            LogReadingsStillFailing(logger, nameof(FroniusMonitorBgService), consecutiveFailures);
        return consecutiveFailures;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{ClassName} readiness probe healthy after {Attempt} attempt(s)")]
    private static partial void LogConnectionRecovered(ILogger logger, string className, int attempt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{ClassName} solar readings unavailable, retrying each poll")]
    private static partial void LogReadingsFailing(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{ClassName} solar readings still unavailable ConsecutiveFailures={ConsecutiveFailures}")]
    private static partial void LogReadingsStillFailing(ILogger logger, string className, int consecutiveFailures);

    [LoggerMessage(Level = LogLevel.Information, Message = "{ClassName} solar readings recovered ConsecutiveFailures={ConsecutiveFailures}")]
    private static partial void LogReadingsRecovered(ILogger logger, string className, int consecutiveFailures);

    [LoggerMessage(Level = LogLevel.Trace, Message = "{ClassName} logged datapoint")]
    private static partial void LogDatapointRecorded(ILogger logger, string className);
}
