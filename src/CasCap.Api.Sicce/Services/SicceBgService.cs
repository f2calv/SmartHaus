namespace CasCap.Services;

/// <summary>Background service for monitoring Sicce devices and publishing events to sinks.</summary>
public sealed partial class SicceBgService(
    ILogger<SicceBgService> logger,
    IOptions<SicceConfig> sicceConfig,
    IHostEnvironment env,
    TimeProvider timeProvider,
    SicceConnectionHealthCheck sicceConnectionHealthCheck,
    SicceClientService sicceClientSvc,
    IEnumerable<IEventSink<SicceEvent>> eventSinks
    ) : IBgFeature
{
    /// <inheritdoc/>
    public string FeatureName => "Sicce";

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (eventSinks.IsNullOrEmpty()) throw new ConfigurationException($"no {nameof(IEventSink<SicceEvent>)} is configured!");
        logger.LogInformation("{ClassName} starting", nameof(SicceBgService));
        try
        {
            await RunServiceAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException) { throw; }
        logger.LogInformation("{ClassName} exiting", nameof(SicceBgService));
    }

    private async Task RunServiceAsync(CancellationToken cancellationToken)
    {
        foreach (var eventSink in eventSinks)
            await eventSink.InitializeAsync(cancellationToken).ConfigureAwait(false);

        var attempt = 1;
        var consecutiveFailures = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (env.IsDevelopment() || sicceConnectionHealthCheck.ConnectionActive)
            {
                if (attempt > 1)
                    LogConnectionRecovered(logger, nameof(SicceBgService), attempt);
                consecutiveFailures = await RecordDataPointsAsync(consecutiveFailures, cancellationToken).ConfigureAwait(false);
                await Task.Delay(sicceConfig.Value.PollingIntervalMs, cancellationToken).ConfigureAwait(false);
                attempt = 1;
            }
            else
            {
                logger.Log(attempt % sicceConfig.Value.ConnectionLogEscalationInterval == 0 ? LogLevel.Warning : LogLevel.Trace,
                    "{ClassName} readiness probe not yet healthy, attempt {Attempt}, retry in {RetryMs}ms...",
                    nameof(SicceBgService), attempt, sicceConfig.Value.ConnectionPollingDelayMs);
                await Task.Delay(sicceConfig.Value.ConnectionPollingDelayMs, cancellationToken).ConfigureAwait(false);
                attempt++;
            }
        }

    }

    private async Task<int> RecordDataPointsAsync(int consecutiveFailures, CancellationToken cancellationToken)
    {
        var deviceInfo = await sicceClientSvc.GetDeviceInfo().ConfigureAwait(false);
        if (deviceInfo is not null)
        {
            var sicceEvent = new SicceEvent(deviceInfo, timeProvider.GetUtcNow().UtcDateTime);
            if (consecutiveFailures > 0)
            {
                LogDeviceInfoRecovered(logger, nameof(SicceBgService), consecutiveFailures);
                consecutiveFailures = 0;
            }

            var tasks = new List<Task>(eventSinks.Count());
            foreach (var eventSink in eventSinks)
                tasks.Add(eventSink.WriteEvent(sicceEvent, cancellationToken));
            await Task.WhenAll(tasks.ToArray()).ConfigureAwait(false);
        }
        else if (consecutiveFailures++ == 0)
            LogDeviceInfoFailing(logger, nameof(SicceBgService));
        else
            LogDeviceInfoStillFailing(logger, nameof(SicceBgService), consecutiveFailures);
        return consecutiveFailures;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{ClassName} readiness probe healthy after {Attempt} attempt(s)")]
    private static partial void LogConnectionRecovered(ILogger logger, string className, int attempt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{ClassName} device info unavailable, retrying each poll")]
    private static partial void LogDeviceInfoFailing(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{ClassName} device info still unavailable ConsecutiveFailures={ConsecutiveFailures}")]
    private static partial void LogDeviceInfoStillFailing(ILogger logger, string className, int consecutiveFailures);

    [LoggerMessage(Level = LogLevel.Information, Message = "{ClassName} device info recovered ConsecutiveFailures={ConsecutiveFailures}")]
    private static partial void LogDeviceInfoRecovered(ILogger logger, string className, int consecutiveFailures);
}
