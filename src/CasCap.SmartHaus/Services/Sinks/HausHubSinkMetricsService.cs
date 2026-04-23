namespace CasCap.Services;

/// <summary>
/// Hub-side event sink that publishes <see cref="HubEvent"/> message counts as OpenTelemetry
/// counter metrics.  To avoid flooding Prometheus with high-frequency individual increments,
/// counts are accumulated locally and flushed to the instrument once every <see cref="BatchSize"/>
/// events (across all types), and also periodically via a background timer.
/// </summary>
/// <remarks>
/// A single counter named <c>{metricNamePrefix}.signalrhub.events</c> is incremented per batch,
/// tagged with <c>event_type</c> and <c>hub_name</c>.
/// </remarks>
[SinkType("Metrics")]
public class HausHubSinkMetricsService : IEventSink<HubEvent>
{
    private const string MetricName = "signalrhub.events";

    private readonly ILogger _logger;
    private readonly string _hausName;
    private readonly Counter<long> _counter;
    private readonly int _batchSize;
    private readonly TimeSpan _flushInterval;

    // Pending per-type counts guarded by _lock
    private readonly Dictionary<string, long> _pending = [];
    private long _pendingTotal;
    private readonly Lock _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="HausHubSinkMetricsService"/> class.
    /// </summary>
    public HausHubSinkMetricsService(ILogger<HausHubSinkMetricsService> logger,
        IOptions<AppConfig> appConfig,
        IOptions<SignalRHubConfig> hubConfig,
        IMeterFactory meterFactory)
    {
        _logger = logger;
        var metricNamePrefix = appConfig.Value.MetricNamePrefix;
        _hausName = appConfig.Value.HausName;
        _batchSize = hubConfig.Value.MetricsBatchSize;
        _flushInterval = TimeSpan.FromMilliseconds(hubConfig.Value.MetricsFlushIntervalMs);

        var meter = meterFactory.Create(metricNamePrefix);
        _counter = meter.CreateCounter<long>(
            $"{metricNamePrefix}.{MetricName}",
            unit: "{events}",
            description: "Number of events received by the HausHub SignalR hub, per event type.");

        _logger.LogInformation("{ClassName} registered counter {MetricName}",
            nameof(HausHubSinkMetricsService), $"{metricNamePrefix}.{MetricName}");
    }

    /// <inheritdoc/>
    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(() => FlushLoopAsync(cancellationToken), cancellationToken);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task WriteEvent(HubEvent @event, CancellationToken cancellationToken = default)
    {
        bool shouldFlush;
        lock (_lock)
        {
            _pending[@event.EventType] = (_pending.TryGetValue(@event.EventType, out var c) ? c : 0) + 1;
            shouldFlush = ++_pendingTotal >= _batchSize;
        }

        if (shouldFlush)
            FlushAll();

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<HubEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    #region private helpers

    private async Task FlushLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_flushInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            FlushAll();
        }

        // Drain any remaining counts on shutdown
        FlushAll();
    }

    private void FlushAll()
    {
        Dictionary<string, long> snapshot;
        lock (_lock)
        {
            if (_pendingTotal == 0)
                return;

            snapshot = new Dictionary<string, long>(_pending);
            _pending.Clear();
            _pendingTotal = 0;
        }

        foreach (var (eventType, count) in snapshot)
        {
            if (count > 0)
                _counter.Add(count,
                    new KeyValuePair<string, object?>("event_type", eventType),
                    new KeyValuePair<string, object?>("hub_name", _hausName));
        }
    }

    #endregion
}
