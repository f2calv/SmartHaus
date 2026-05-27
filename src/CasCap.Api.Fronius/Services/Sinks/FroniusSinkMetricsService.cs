namespace CasCap.Services;

/// <summary>
/// Event sink that publishes <see cref="FroniusEvent"/> values as OpenTelemetry gauge metrics.
/// One gauge is created per <see cref="FroniusFunction"/> value decorated with
/// <see cref="MetricAttribute"/>. Metric names, units, and descriptions are all sourced
/// from the enum attribute, centralising configuration next to the <see cref="FroniusFunction"/>
/// definition. The enum value names match the corresponding <see cref="FroniusEvent"/> property
/// names, enabling property access via reflection.
/// </summary>
[SinkType("Metrics")]
public sealed class FroniusSinkMetricsService : IEventSink<FroniusEvent>
{
    /// <inheritdoc/>
    public string SinkType => "Metrics";

    private readonly ILogger _logger;
    private readonly Dictionary<string, Measurement<double>> _measurements = [];
    private readonly FrozenDictionary<string, Func<FroniusEvent, double>> _propertyAccessors;

    /// <summary>
    /// Initializes a new instance of the <see cref="FroniusSinkMetricsService"/> class.
    /// </summary>
    public FroniusSinkMetricsService(ILogger<FroniusSinkMetricsService> logger,
        IOptions<FroniusConfig> froniusConfig,
        IMeterFactory meterFactory)
    {
        _logger = logger;
        var metricNamePrefix = froniusConfig.Value.MetricNamePrefix;
        var meter = meterFactory.Create(metricNamePrefix);
        var propertyAccessors = new Dictionary<string, Func<FroniusEvent, double>>();

        foreach (var function in Enum.GetValues<FroniusFunction>())
        {
            var field = typeof(FroniusFunction).GetField(function.ToString())!;
            var metricAttr = field.GetCustomAttribute<MetricAttribute>();
            if (metricAttr is null)
                continue;

            var propName = function.ToString();
            var prop = typeof(FroniusEvent).GetProperty(propName);
            if (prop is null)
                continue;

            _measurements[propName] = default;
            propertyAccessors[propName] = CreateAccessor(prop);

            var metricName = $"{metricNamePrefix}.{metricAttr.Name}";
            meter.CreateObservableGauge(
                metricName,
                () => _measurements[propName],
                unit: metricAttr.Unit,
                description: metricAttr.Description);

            _logger.LogInformation("{ClassName} registered gauge {MetricName} for {Property}",
                nameof(FroniusSinkMetricsService), metricName, propName);
        }

        _propertyAccessors = propertyAccessors.ToFrozenDictionary();
    }

    /// <inheritdoc/>
    public Task WriteEvent(FroniusEvent @event, CancellationToken cancellationToken = default)
    {
        foreach (var (name, accessor) in _propertyAccessors)
        {
            _measurements[name] = new(accessor(@event),
                new KeyValuePair<string, object?>("property", name));
        }

        return Task.CompletedTask;
    }


    #region private helpers

    private static Func<FroniusEvent, double> CreateAccessor(PropertyInfo prop)
    {
        var getter = prop.GetGetMethod()!;
        return e => (double)getter.Invoke(e, null)!;
    }

    #endregion
}
