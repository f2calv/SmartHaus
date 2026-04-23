namespace CasCap.Services;

/// <summary>
/// Event sink that publishes <see cref="EdgeHardwareEvent"/> values as OpenTelemetry gauge metrics.
/// GPU gauges (power, temperature, utilization, memory) are updated only when an NVIDIA GPU
/// is detected; the CPU temperature gauge is always updated when available.
/// </summary>
[SinkType("Metrics")]
internal class EdgeHardwareSinkMetricsService : IEventSink<EdgeHardwareEvent>
{
    private readonly ILogger _logger;
    private Measurement<double> _gpuPower;
    private Measurement<double> _gpuTemp;
    private Measurement<double> _gpuUtil;
    private Measurement<double> _gpuMemUsed;
    private Measurement<double> _cpuTemp;

    /// <summary>
    /// Initializes a new instance of the <see cref="EdgeHardwareSinkMetricsService"/> class.
    /// </summary>
    public EdgeHardwareSinkMetricsService(ILogger<EdgeHardwareSinkMetricsService> logger,
        IOptions<EdgeHardwareConfig> edgeHardwareConfig,
        IOptions<KubeAppConfig> kubeAppConfig,
        IMeterFactory meterFactory)
    {
        _logger = logger;
        var prefix = edgeHardwareConfig.Value.MetricNamePrefix;
        var meter = meterFactory.Create(prefix);
        var tags = new KeyValuePair<string, object?>[]
        {
            new(nameof(KubeAppConfig.NodeName), kubeAppConfig.Value.NodeName)
        };

        meter.CreateObservableGauge($"{prefix}.gpu.power", () => _gpuPower,
            unit: "W", description: "GPU power draw", tags: tags);

        meter.CreateObservableGauge($"{prefix}.gpu.temperature", () => _gpuTemp,
            unit: "Cel", description: "GPU temperature", tags: tags);

        meter.CreateObservableGauge($"{prefix}.gpu.utilization", () => _gpuUtil,
            unit: "%", description: "GPU compute utilization", tags: tags);

        meter.CreateObservableGauge($"{prefix}.gpu.memory.used", () => _gpuMemUsed,
            unit: "MiBy", description: "GPU memory used", tags: tags);

        meter.CreateObservableGauge($"{prefix}.hw.temperature", () => _cpuTemp,
            unit: "Cel", description: "CPU temperature", tags: tags);

        _logger.LogInformation("{ClassName} registered edge hardware gauges under {Prefix}",
            nameof(EdgeHardwareSinkMetricsService), prefix);
    }

    /// <inheritdoc/>
    public Task WriteEvent(EdgeHardwareEvent @event, CancellationToken cancellationToken = default)
    {
        // GPU gauges — optional, only populated when nvidia-smi returns data
        if (@event.GpuPowerDrawW.HasValue)
            _gpuPower = new(@event.GpuPowerDrawW.Value);
        if (@event.GpuTemperatureC.HasValue)
            _gpuTemp = new(@event.GpuTemperatureC.Value);
        if (@event.GpuUtilizationPercent.HasValue)
            _gpuUtil = new(@event.GpuUtilizationPercent.Value);
        if (@event.GpuMemoryUsedMiB.HasValue)
            _gpuMemUsed = new(@event.GpuMemoryUsedMiB.Value);

        // CPU gauge — updated only when a reading is available
        if (@event.CpuTemperatureC.HasValue)
            _cpuTemp = new(@event.CpuTemperatureC.Value);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<EdgeHardwareEvent> GetEvents(string? id = null, int limit = 1000,
        CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
