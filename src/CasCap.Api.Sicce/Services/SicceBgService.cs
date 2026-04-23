namespace CasCap.Services;

/// <summary>Background service for monitoring Sicce devices and publishing events to sinks.</summary>
public class SicceBgService(
    ILogger<SicceBgService> logger,
    IOptions<SicceConfig> sicceConfig,
    IHostEnvironment env,
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
        if (eventSinks.IsNullOrEmpty()) throw new GenericException($"no {nameof(IEventSink<SicceEvent>)} is configured!");
        logger.LogInformation("{ClassName} starting", nameof(SicceBgService));
        try
        {
            await RunServiceAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException) { throw; }
        logger.LogInformation("{ClassName} exiting", nameof(SicceBgService));
    }

    private async Task RunServiceAsync(CancellationToken cancellationToken)
    {
        foreach (var eventSink in eventSinks)
            await eventSink.InitializeAsync(cancellationToken);

        var attempt = 1;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (env.IsDevelopment() || sicceConnectionHealthCheck.ConnectionActive)
            {
                await RecordDataPoints();
                await Task.Delay(sicceConfig.Value.PollingIntervalMs, cancellationToken);
                attempt = 1;
            }
            else
            {
                logger.Log(attempt % sicceConfig.Value.ConnectionLogEscalationInterval == 0 ? LogLevel.Warning : LogLevel.Trace,
                    "{ClassName} readiness probe not yet healthy, attempt {Attempt}, retry in {RetryMs}ms...",
                    nameof(SicceBgService), attempt, sicceConfig.Value.ConnectionPollingDelayMs);
                await Task.Delay(sicceConfig.Value.ConnectionPollingDelayMs, cancellationToken);
                attempt++;
            }
        }

        async Task RecordDataPoints()
        {
            var deviceInfo = await sicceClientSvc.GetDeviceInfo();
            if (deviceInfo is not null)
            {
                var sicceEvent = new SicceEvent(deviceInfo);

                var tasks = new List<Task>(eventSinks.Count());
                foreach (var eventSink in eventSinks)
                    tasks.Add(eventSink.WriteEvent(sicceEvent, cancellationToken));
                await Task.WhenAll(tasks.ToArray());
            }
            else
                logger.LogWarning("{ClassName} get device info returns a null object", nameof(SicceBgService));
        }
    }
}
