namespace CasCap.Services;

/// <summary>
/// Manipulates endpoint+event data from the Buderus KM200 in conjunction with EventSinks.
/// </summary>
public sealed partial class BuderusKm200MonitorBgService(
    ILogger<BuderusKm200MonitorBgService> logger,
    IOptions<BuderusConfig> buderusConfig,
    TimeProvider timeProvider,
    BuderusKm200ConnectionHealthCheck km200ConnectionHealthCheck,
    BuderusKm200ClientService buderusKm200ClientSvc,
    IEnumerable<IEventSink<BuderusEvent>> eventSinks
    ) : IBgFeature
{

    private readonly Dictionary<string, (int ConsecutiveFailures, DateTimeOffset FailingSince)> _failingDatapoints = [];

    /// <inheritdoc/>
    public string FeatureName => "Buderus";

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (eventSinks.IsNullOrEmpty()) throw new ConfigurationException($"no {nameof(IEventSink<BuderusEvent>)} is configured!");
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("{ClassName} starting", nameof(BuderusKm200MonitorBgService));
        try
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("{ClassName} endpoint is '{Endpoint}'", nameof(BuderusKm200MonitorBgService), buderusKm200ClientSvc.Client.BaseAddress);
            await RunServiceAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException) { throw; }
        if (logger.IsEnabled(LogLevel.Information))
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

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("{ClassName} polling {Count} configured datapoints",
                nameof(BuderusKm200MonitorBgService), buderusConfig.Value.DatapointMappings.Count);
        if (logger.IsEnabled(LogLevel.Debug))
            foreach (var (id, column) in buderusConfig.Value.DatapointMappings)
                logger.LogDebug("{ClassName} configured datapoint {DatapointId} -> {ColumnName}", nameof(BuderusKm200MonitorBgService), id, column);

        foreach (var eventSink in eventSinks)
            await eventSink.InitializeAsync(cancellationToken).ConfigureAwait(false);

        var attempt = 1;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (km200ConnectionHealthCheck.ConnectionActive)
            {
                if (attempt > 1)
                    LogConnectionRecovered(logger, nameof(BuderusKm200MonitorBgService), attempt);
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
                    LogDatapointRecorded(logger, nameof(BuderusKm200MonitorBgService), buderusEvent);
                    OnDatapointSucceeded(datapointId);
                }
                else
                    OnDatapointFailed(datapointId);
                //TODO: investigate how low DatapointDelayMs can be reduced without overwhelming the KM200 gateway
                await Task.Delay(buderusConfig.Value.DatapointDelayMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void OnDatapointFailed(string datapointId)
    {
        if (_failingDatapoints.TryGetValue(datapointId, out var failure))
        {
            _failingDatapoints[datapointId] = (failure.ConsecutiveFailures + 1, failure.FailingSince);
            LogDatapointStillFailing(logger, nameof(BuderusKm200MonitorBgService), datapointId, failure.ConsecutiveFailures + 1);
            return;
        }
        _failingDatapoints[datapointId] = (1, timeProvider.GetUtcNow());
        LogDatapointFailing(logger, nameof(BuderusKm200MonitorBgService), datapointId);
    }

    private void OnDatapointSucceeded(string datapointId)
    {
        if (!_failingDatapoints.Remove(datapointId, out var failure))
            return;
        LogDatapointRecovered(logger, nameof(BuderusKm200MonitorBgService), datapointId,
            failure.ConsecutiveFailures, timeProvider.GetUtcNow() - failure.FailingSince);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{ClassName} readiness probe healthy after {Attempt} attempt(s)")]
    private static partial void LogConnectionRecovered(ILogger logger, string className, int attempt);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{ClassName} logged datapoint {@BuderusEvent}")]
    private static partial void LogDatapointRecorded(ILogger logger, string className, BuderusEvent buderusEvent);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{ClassName} datapoint {DatapointId} returned no value, retrying each poll")]
    private static partial void LogDatapointFailing(ILogger logger, string className, string datapointId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{ClassName} datapoint {DatapointId} still returning no value ConsecutiveFailures={ConsecutiveFailures}")]
    private static partial void LogDatapointStillFailing(ILogger logger, string className, string datapointId, int consecutiveFailures);

    [LoggerMessage(Level = LogLevel.Information, Message = "{ClassName} datapoint {DatapointId} recovered ConsecutiveFailures={ConsecutiveFailures} Downtime={Downtime}")]
    private static partial void LogDatapointRecovered(ILogger logger, string className, string datapointId, int consecutiveFailures, TimeSpan downtime);
}
