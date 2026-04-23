namespace CasCap.Services;

/// <summary>
/// Event sink that publishes <see cref="DoorBirdEvent"/> counts as OpenTelemetry gauge metrics.
/// One gauge is created per <see cref="DoorBirdEventType"/> value decorated with
/// <see cref="MetricAttribute"/>, incremented on each matching event.
/// Metric names, units, and descriptions are all defined on <see cref="DoorBirdEventType"/>
/// via <see cref="MetricAttribute"/>.
/// </summary>
[SinkType("Metrics")]
public class DoorBirdSinkMetricsService : IEventSink<DoorBirdEvent>
{
    private readonly ILogger _logger;
    private readonly Dictionary<DoorBirdEventType, Measurement<double>> _measurements = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="DoorBirdSinkMetricsService"/> class.
    /// </summary>
    public DoorBirdSinkMetricsService(ILogger<DoorBirdSinkMetricsService> logger,
        IOptions<DoorBirdConfig> doorBirdConfig,
        IMeterFactory meterFactory)
    {
        _logger = logger;
        var metricNamePrefix = doorBirdConfig.Value.MetricNamePrefix;
        var meter = meterFactory.Create(metricNamePrefix);

        foreach (var eventType in Enum.GetValues<DoorBirdEventType>())
        {
            var field = typeof(DoorBirdEventType).GetField(eventType.ToString())!;
            var metricAttr = field.GetCustomAttribute<MetricAttribute>();
            if (metricAttr is null)
                continue;

            RegisterGauge(meter, metricNamePrefix, eventType, metricAttr);
        }
    }

    /// <inheritdoc/>
    public Task WriteEvent(DoorBirdEvent @event, CancellationToken cancellationToken = default)
    {
        if (_measurements.TryGetValue(@event.DoorBirdEventType, out var current))
        {
            _measurements[@event.DoorBirdEventType] = new(current.Value + 1,
                new KeyValuePair<string, object?>("event.type", @event.DoorBirdEventType.ToString()));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<DoorBirdEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    #region private helpers

    private void RegisterGauge(Meter meter, string metricNamePrefix, DoorBirdEventType eventType,
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
            nameof(DoorBirdSinkMetricsService), metricName, eventType);
    }

    #endregion
}
