namespace CasCap.Services;

/// <summary>
/// Event sink that publishes <see cref="BuderusEvent"/> values as OpenTelemetry gauge metrics.
/// Gauge definitions are driven by <see cref="DatapointMapping.MetricName"/> in
/// <see cref="BuderusConfig.DatapointMappings"/>.
/// </summary>
[SinkType("Metrics")]
public class BuderusSinkMetricsService : IEventSink<BuderusEvent>
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, Dictionary<string, Measurement<double>>> _measurementsByMetric = [];
    private readonly Dictionary<string, string> _datapointToMetricKey = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="BuderusSinkMetricsService"/> class.
    /// </summary>
    public BuderusSinkMetricsService(ILogger<BuderusSinkMetricsService> logger,
        IOptions<BuderusConfig> config,
        IMeterFactory meterFactory)
    {
        _logger = logger;
        var meter = meterFactory.Create(config.Value.MetricNamePrefix);

        foreach (var (datapointId, mapping) in config.Value.DatapointMappings)
        {
            if (mapping.MetricName is null)
                continue;

            _datapointToMetricKey[datapointId] = mapping.MetricName;

            if (!_measurementsByMetric.ContainsKey(mapping.MetricName))
            {
                var metricName = mapping.MetricName;
                _measurementsByMetric[metricName] = [];
                meter.CreateObservableGauge(
                    metricName,
                    () => _measurementsByMetric[metricName].Values,
                    unit: mapping.MetricUnit,
                    description: mapping.MetricDescription);

                _logger.LogInformation("{ClassName} registered gauge {MetricName}",
                    nameof(BuderusSinkMetricsService), metricName);
            }

            _logger.LogInformation("{ClassName} tracking {DatapointId} under gauge {MetricName}",
                nameof(BuderusSinkMetricsService), datapointId, mapping.MetricName);
        }
    }

    /// <inheritdoc/>
    public Task WriteEvent(BuderusEvent @event, CancellationToken cancellationToken = default)
    {
        if (_datapointToMetricKey.TryGetValue(@event.Id, out var metricKey)
            && double.TryParse(@event.Value, out var value))
        {
            _measurementsByMetric[metricKey][@event.Id] = new(value,
                new KeyValuePair<string, object?>("datapoint.id", @event.Id));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<BuderusEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
