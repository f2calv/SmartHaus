namespace CasCap.Services;

/// <summary>
/// Event sink that publishes <see cref="KnxEvent"/> values as OpenTelemetry gauge metrics.
/// Tracks function enum values decorated with <see cref="MetricAttribute"/> across
/// <see cref="GroupAddressCategory.HZ"/>, <see cref="GroupAddressCategory.LI"/>,
/// <see cref="GroupAddressCategory.SD"/>, <see cref="GroupAddressCategory.BI"/> and
/// <see cref="GroupAddressCategory.BL"/> categories.
/// Numeric values are emitted directly; boolean state values use
/// <see cref="KnxEvent.Value"/> (a <see cref="bool"/> for DPT 1.x types) to map
/// <see langword="true"/> to <c>1</c> and <see langword="false"/> to <c>0</c>.
/// Gauges are created lazily as new group addresses are discovered at runtime.
/// </summary>
[SinkType("Metrics")]
public class KnxSinkMetricsService : IEventSink<KnxEvent>
{
    private readonly ILogger _logger;
    private readonly Meter _meter;
    private readonly string _metricNamePrefix;
    private readonly Dictionary<string, Dictionary<string, Measurement<double>>> _measurementsByMetric = [];

    /// <summary>
    /// Metric configuration keyed by function name string, derived at startup from
    /// <see cref="MetricAttribute"/> decorations on the KNX function enums.
    /// </summary>
    private static readonly FrozenDictionary<string, MetricAttribute> MetricsByFunction =
        BuildMetricsByFunction();

    private static FrozenDictionary<string, MetricAttribute> BuildMetricsByFunction()
    {
        var dict = new Dictionary<string, MetricAttribute>();
        var enumTypes = new[]
        {
            typeof(HvacFunction),
            typeof(LightingFunction),
            typeof(PowerOutletFunction),
            typeof(ContactFunction),
            typeof(ShutterFunction),
        };
        foreach (var type in enumTypes)
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var attr = field.GetCustomAttribute<MetricAttribute>();
                if (attr is not null)
                    dict[field.Name] = attr;
            }
        }
        return dict.ToFrozenDictionary();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KnxSinkMetricsService"/> class.
    /// </summary>
    public KnxSinkMetricsService(ILogger<KnxSinkMetricsService> logger,
        IOptions<KnxConfig> knxConfig,
        IMeterFactory meterFactory)
    {
        _logger = logger;
        _metricNamePrefix = knxConfig.Value.MetricNamePrefix;
        _meter = meterFactory.Create(_metricNamePrefix);
    }

    /// <inheritdoc/>
    public Task WriteEvent(KnxEvent @event, CancellationToken cancellationToken = default)
    {
        var function = @event.Kga.Function;
        if (function is null || !MetricsByFunction.TryGetValue(function, out var metricAttr))
            return Task.CompletedTask;

        double value;
        if (metricAttr.IsBoolean)
        {
            if (@event.Value is not bool state)
                return Task.CompletedTask;
            value = state ? 1.0 : 0.0;
        }
        else if (!double.TryParse(@event.ValueAsString, out value))
            return Task.CompletedTask;

        var metricKey = metricAttr.Name;
        if (!_measurementsByMetric.ContainsKey(metricKey))
            RegisterGauge(metricKey, metricAttr);

        var name = @event.Kga.Name;
        if (!_measurementsByMetric[metricKey].ContainsKey(name))
            _logger.LogTrace("{ClassName} tracking {GroupAddressName} ({GroupAddress}) under gauge {MetricName}",
                nameof(KnxSinkMetricsService), name, @event.Kga.GroupAddress, $"{_metricNamePrefix}.{metricKey}");

        _measurementsByMetric[metricKey][name] = new(value,
            new KeyValuePair<string, object?>("group.address.name", name),
            new KeyValuePair<string, object?>("group.address", @event.Kga.GroupAddress),
            new KeyValuePair<string, object?>("function", function));

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<KnxEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    #region private helpers

    private void RegisterGauge(string metricKey, MetricAttribute metricAttr)
    {
        _measurementsByMetric[metricKey] = [];
        var metricName = $"{_metricNamePrefix}.{metricKey}";
        _meter.CreateObservableGauge(
            metricName,
            () => _measurementsByMetric[metricKey].Values,
            unit: metricAttr.Unit,
            description: $"KNX {metricKey} readings");

        _logger.LogInformation("{ClassName} registered gauge {MetricName}",
            nameof(KnxSinkMetricsService), metricName);
    }

    #endregion
}
