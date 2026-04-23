namespace CasCap.Services;

/// <summary>
/// Event sink that publishes <see cref="SicceEvent"/> values as OpenTelemetry gauge metrics.
/// One gauge is created per <see cref="SicceFunction"/> value decorated with
/// <see cref="MetricAttribute"/>. Metric names, units, and descriptions are all sourced
/// from the enum attribute, centralising configuration next to the <see cref="SicceFunction"/>
/// definition. The enum value names match the corresponding <see cref="SicceEvent"/> property
/// names, enabling property access via reflection.
/// </summary>
[SinkType("Metrics")]
public class SicceSinkMetricsService : IEventSink<SicceEvent>
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, Measurement<double>> _measurements = [];
    private readonly Dictionary<string, Func<SicceEvent, double>> _propertyAccessors = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="SicceSinkMetricsService"/> class.
    /// </summary>
    public SicceSinkMetricsService(ILogger<SicceSinkMetricsService> logger,
        IOptions<SicceConfig> sicceConfig,
        IMeterFactory meterFactory)
    {
        _logger = logger;
        var metricNamePrefix = sicceConfig.Value.MetricNamePrefix;
        var meter = meterFactory.Create(metricNamePrefix);

        foreach (var function in Enum.GetValues<SicceFunction>())
        {
            var field = typeof(SicceFunction).GetField(function.ToString())!;
            var metricAttr = field.GetCustomAttribute<MetricAttribute>();
            if (metricAttr is null)
                continue;

            var propName = function.ToString();
            var prop = typeof(SicceEvent).GetProperty(propName);
            if (prop is null)
                continue;

            _measurements[propName] = default;
            _propertyAccessors[propName] = CreateAccessor(prop);

            var metricName = $"{metricNamePrefix}.{metricAttr.Name}";
            meter.CreateObservableGauge(
                metricName,
                () => _measurements[propName],
                unit: metricAttr.Unit,
                description: metricAttr.Description);

            _logger.LogInformation("{ClassName} registered gauge {MetricName} for {Property}",
                nameof(SicceSinkMetricsService), metricName, propName);
        }
    }

    /// <inheritdoc/>
    public Task WriteEvent(SicceEvent @event, CancellationToken cancellationToken = default)
    {
        foreach (var (name, accessor) in _propertyAccessors)
        {
            _measurements[name] = new(accessor(@event),
                new KeyValuePair<string, object?>("property", name));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<SicceEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    #region private helpers

    private static Func<SicceEvent, double> CreateAccessor(PropertyInfo prop)
    {
        var getter = prop.GetGetMethod()!;
        return e => Convert.ToDouble(getter.Invoke(e, null));
    }

    #endregion
}
