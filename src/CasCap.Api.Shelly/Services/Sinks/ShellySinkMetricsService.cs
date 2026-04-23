namespace CasCap.Services;

/// <summary>
/// Event sink that publishes <see cref="ShellyEvent"/> values as OpenTelemetry gauge metrics.
/// One gauge is created per <see cref="ShellyFunction"/> value decorated with
/// <see cref="MetricAttribute"/>. Metric names, units, and descriptions are all sourced
/// from the enum attribute, centralising configuration next to the <see cref="ShellyFunction"/>
/// definition. The enum value names match the corresponding <see cref="ShellyEvent"/> property
/// names, enabling property access via reflection.
/// </summary>
[SinkType("Metrics")]
public class ShellySinkMetricsService : IEventSink<ShellyEvent>
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, Measurement<double>> _measurements = [];
    private readonly Dictionary<string, Func<ShellyEvent, double>> _propertyAccessors = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellySinkMetricsService"/> class.
    /// </summary>
    public ShellySinkMetricsService(ILogger<ShellySinkMetricsService> logger,
        IOptions<ShellyConfig> shellyConfig,
        IMeterFactory meterFactory)
    {
        _logger = logger;
        var metricNamePrefix = shellyConfig.Value.MetricNamePrefix;
        var meter = meterFactory.Create(metricNamePrefix);

        foreach (var function in Enum.GetValues<ShellyFunction>())
        {
            var field = typeof(ShellyFunction).GetField(function.ToString())!;
            var metricAttr = field.GetCustomAttribute<MetricAttribute>();
            if (metricAttr is null)
                continue;

            var propName = function.ToString();
            var prop = typeof(ShellyEvent).GetProperty(propName);
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
                nameof(ShellySinkMetricsService), metricName, propName);
        }
    }

    /// <inheritdoc/>
    public Task WriteEvent(ShellyEvent @event, CancellationToken cancellationToken = default)
    {
        foreach (var (name, accessor) in _propertyAccessors)
        {
            _measurements[name] = new(accessor(@event),
                new KeyValuePair<string, object?>("property", name),
                new KeyValuePair<string, object?>("device_id", @event.DeviceId));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<ShellyEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    #region private helpers

    private static Func<ShellyEvent, double> CreateAccessor(PropertyInfo prop)
    {
        var getter = prop.GetGetMethod()!;
        return e => (double)getter.Invoke(e, null)!;
    }

    #endregion
}
