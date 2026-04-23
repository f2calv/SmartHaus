namespace CasCap.Services;

/// <summary>
/// Event sink that publishes <see cref="UbiquitiEvent"/> counts as OpenTelemetry gauge metrics.
/// One gauge is created per <see cref="UbiquitiEventType"/> value decorated with
/// <see cref="MetricAttribute"/>, incremented on each matching event.
/// Metric names, units, and descriptions are all defined on <see cref="UbiquitiEventType"/>
/// via <see cref="MetricAttribute"/>.
/// </summary>
[SinkType("Metrics")]
public class UbiquitiSinkMetricsService : IEventSink<UbiquitiEvent>
{
    private readonly ILogger _logger;
    private readonly Dictionary<UbiquitiEventType, Measurement<double>> _measurements = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="UbiquitiSinkMetricsService"/> class.
    /// </summary>
    public UbiquitiSinkMetricsService(ILogger<UbiquitiSinkMetricsService> logger,
        IOptions<UbiquitiConfig> ubiquitiConfig,
        IMeterFactory meterFactory)
    {
        _logger = logger;
        var metricNamePrefix = ubiquitiConfig.Value.MetricNamePrefix;
        var meter = meterFactory.Create(metricNamePrefix);

        foreach (var eventType in Enum.GetValues<UbiquitiEventType>())
        {
            var field = typeof(UbiquitiEventType).GetField(eventType.ToString())!;
            var metricAttr = field.GetCustomAttribute<MetricAttribute>();
            if (metricAttr is null)
                continue;

            RegisterGauge(meter, metricNamePrefix, eventType, metricAttr);
        }
    }

    /// <inheritdoc/>
    public Task WriteEvent(UbiquitiEvent @event, CancellationToken cancellationToken = default)
    {
        if (_measurements.TryGetValue(@event.UbiquitiEventType, out var current))
        {
            _measurements[@event.UbiquitiEventType] = new(current.Value + 1,
                new KeyValuePair<string, object?>("event.type", @event.UbiquitiEventType.ToString()));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<UbiquitiEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    #region private helpers

    private void RegisterGauge(Meter meter, string metricNamePrefix, UbiquitiEventType eventType,
        MetricAttribute metricAttr)
    {
        _measurements[eventType] = new(0,
            new KeyValuePair<string, object?>("event.type", eventType.ToString()));

        var metricName = $"{metricNamePrefix}.{metricAttr.Name}";
        meter.CreateObservableGauge(
            metricName,
            () => _measurements[eventType],
            unit: metricAttr.Unit,
            description: metricAttr.Description);

        _logger.LogInformation("{ClassName} registered gauge {MetricName} for {EventType}",
            nameof(UbiquitiSinkMetricsService), metricName, eventType);
    }

    #endregion
}
