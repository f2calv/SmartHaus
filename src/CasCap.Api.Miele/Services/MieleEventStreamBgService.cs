namespace CasCap.Services;

/// <summary>Background service for the Miele Server-Sent Events stream.</summary>
public class MieleEventStreamBgService(
    ILogger<MieleEventStreamBgService> logger,
    IOptions<MieleConfig> mieleConfig,
    MieleConnectionHealthCheck mieleConnectionHealthCheck,
    IHttpClientFactory httpClientFactory,
    IEnumerable<IEventSink<MieleEvent>> sinks
    ) : IBgFeature
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient(nameof(MieleConnectionHealthCheck));

    /// <inheritdoc/>
    public string FeatureName => "Miele";

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{ClassName} starting", nameof(MieleEventStreamBgService));
        foreach (var sink in sinks)
            await sink.InitializeAsync(cancellationToken);
        try
        {
            await RunServiceAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException) { throw; }
        logger.LogInformation("{ClassName} exiting", nameof(MieleEventStreamBgService));
    }

    private async Task RunServiceAsync(CancellationToken cancellationToken)
    {
        var attempt = 1;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (mieleConnectionHealthCheck.ConnectionActive)
            {
                await InitiateEventStream();
                await Task.Delay(mieleConfig.Value.EventStreamReconnectDelayMs, cancellationToken);
                attempt = 1;
            }
            else
            {
                logger.Log(attempt % mieleConfig.Value.ConnectionLogEscalationInterval == 0 ? LogLevel.Warning : LogLevel.Trace,
                    "{ClassName} readiness probe not yet healthy, attempt {Attempt}, retry in {RetryMs}ms...",
                    nameof(MieleEventStreamBgService), attempt, mieleConfig.Value.ConnectionPollingDelayMs);
                await Task.Delay(mieleConfig.Value.ConnectionPollingDelayMs, cancellationToken);
                attempt++;
            }
        }

        async Task InitiateEventStream()
        {
            try
            {
                using var stream = await _httpClient.GetStreamAsync(mieleConfig.Value.EventStreamUrl, cancellationToken);
                string? json;
                using var reader = new StreamReader(stream);
                while ((json = await reader.ReadLineAsync(cancellationToken)) is not null)
                {
                    if (string.IsNullOrWhiteSpace(json)) { }
                    //else if (json == dataPrefix)
                    //    OnRaiseHeartbeatEvent(new Heartbeat { time = DateTime.UtcNow });
                    else
                    {
                        logger.LogInformation("{ClassName} Miele event received, json={Json}", nameof(MieleEventStreamBgService), json);
                        var evt = new MieleEvent
                        {
                            DeviceId = "unknown",
                            EventType = MieleEventType.StatusUpdate,
                            RawJson = json,
                            TimestampUtc = DateTime.UtcNow,
                        };
                        foreach (var sink in sinks)
                            await sink.WriteEvent(evt, cancellationToken);
                    }

                    if (cancellationToken.IsCancellationRequested)
                        return;
                }
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "{ClassName} SSE stream failure", nameof(MieleEventStreamBgService));
            }
        }
    }
}
