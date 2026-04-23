namespace CasCap.Services;

/// <summary>
/// Unified background service that polls GPU metrics via <c>nvidia-smi</c> and/or CPU
/// temperature via <see cref="ICpuTemperatureProvider"/>, then emits
/// <see cref="EdgeHardwareEvent"/> instances to all registered
/// <see cref="IEventSink{T}"/> implementations.
/// </summary>
/// <remarks>
/// Replaces the former <c>GpuMonitorBgService</c> and <c>CpuMonitorBgService</c>.
/// Registration is gated in Program.cs — this service uses <see cref="IBgFeature.AlwaysEnabled"/>
/// so that <c>FeatureFlagService</c> always starts it once registered.
/// Consumers query metrics via <see cref="IEdgeHardwareQueryService"/> rather than this service directly.
/// </remarks>
public class EdgeHardwareMonitorBgService(ILogger<EdgeHardwareMonitorBgService> logger,
    IOptions<EdgeHardwareConfig> edgeHardwareConfig,
    IKubeAppConfig kubeAppConfig,
    IEnumerable<IEventSink<EdgeHardwareEvent>> eventSinks,
    ICpuTemperatureProvider? cpuTemperatureSvc = null) : IBgFeature
{
    private readonly string _nodeName = kubeAppConfig.NodeName ?? Environment.MachineName;

    // GPU polling is attempted; if nvidia-smi is unavailable it gracefully returns null.
    private readonly bool _gpuEnabled = true;

    private bool _nvidiaSmiFailureLogged;

    private volatile EdgeHardwareSnapshot? _latestSnapshot;

    /// <inheritdoc/>
    public string FeatureName => IBgFeature.AlwaysEnabled;

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{ClassName} starting", nameof(EdgeHardwareMonitorBgService));

        foreach (var sink in eventSinks)
            await sink.InitializeAsync(cancellationToken);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var edgeEvent = CollectMetrics();
                await DispatchToSinks(edgeEvent, cancellationToken);
                await Task.Delay(edgeHardwareConfig.Value.PollIntervalMs, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
        {
            logger.LogCritical(ex, "{ClassName} fatal error", nameof(EdgeHardwareMonitorBgService));
            throw;
        }

        logger.LogInformation("{ClassName} exiting", nameof(EdgeHardwareMonitorBgService));
    }

    /// <summary>
    /// Collects GPU and CPU metrics into a unified <see cref="EdgeHardwareSnapshot"/>
    /// and maps it to an <see cref="EdgeHardwareEvent"/> for sink dispatch.
    /// </summary>
    private EdgeHardwareEvent CollectMetrics()
    {
        var gpuSnapshot = _gpuEnabled ? ReadGpuMetrics() : null;
        var cpuTemp = cpuTemperatureSvc?.GetTempInCelsius();

        _latestSnapshot = gpuSnapshot is not null
            ? gpuSnapshot with { CpuTemperatureC = cpuTemp, NodeName = _nodeName }
            : new EdgeHardwareSnapshot { CpuTemperatureC = cpuTemp, NodeName = _nodeName, Timestamp = DateTimeOffset.UtcNow };

        return new EdgeHardwareEvent
        {
            NodeName = _nodeName,
            TimestampUtc = DateTime.UtcNow,
            GpuPowerDrawW = _latestSnapshot.GpuPowerDrawW,
            GpuTemperatureC = _latestSnapshot.GpuTemperatureC,
            GpuUtilizationPercent = _latestSnapshot.GpuUtilizationPercent,
            GpuMemoryUtilizationPercent = _latestSnapshot.GpuMemoryUtilizationPercent,
            GpuMemoryUsedMiB = _latestSnapshot.GpuMemoryUsedMiB,
            GpuMemoryTotalMiB = _latestSnapshot.GpuMemoryTotalMiB,
            CpuTemperatureC = _latestSnapshot.CpuTemperatureC,
        };
    }

    /// <summary>
    /// Reads GPU metrics via <c>nvidia-smi</c> and returns a GPU-only snapshot.
    /// </summary>
    private EdgeHardwareSnapshot? ReadGpuMetrics()
    {
        try
        {
            var (csv, stderr, exitCode) = ShellExtensions.RunProcessDiagnostic("nvidia-smi",
                "--query-gpu=power.draw,temperature.gpu,utilization.gpu,utilization.memory,memory.used,memory.total --format=csv,noheader,nounits");

            if (string.IsNullOrWhiteSpace(csv) || exitCode != 0)
            {
                if (!_nvidiaSmiFailureLogged)
                {
                    logger.LogWarning("{ClassName} nvidia-smi failed (exitCode={ExitCode}, stderr={StdErr})",
                        nameof(EdgeHardwareMonitorBgService), exitCode, stderr);
                    _nvidiaSmiFailureLogged = true;
                }
                else
                    logger.LogDebug("{ClassName} nvidia-smi unavailable", nameof(EdgeHardwareMonitorBgService));
                return null;
            }

            var snapshot = EdgeHardwareSnapshot.ParseGpuCsv(csv);
            if (snapshot is null)
            {
                logger.LogWarning("{ClassName} failed to parse nvidia-smi output {Output}",
                    nameof(EdgeHardwareMonitorBgService), csv);
                return null;
            }

            logger.LogTrace("{ClassName} logged datapoint {@Data}", nameof(EdgeHardwareMonitorBgService), snapshot);

            return snapshot;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{ClassName} failed to read GPU metrics", nameof(EdgeHardwareMonitorBgService));
            return null;
        }
    }

    /// <summary>
    /// Dispatches an <see cref="EdgeHardwareEvent"/> to all registered sinks.
    /// </summary>
    private async Task DispatchToSinks(EdgeHardwareEvent edgeEvent, CancellationToken cancellationToken)
    {
        var tasks = new List<Task>(eventSinks.Count());
        foreach (var sink in eventSinks)
            tasks.Add(sink.WriteEvent(edgeEvent, cancellationToken));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Runs <c>nvidia-smi</c> and returns the single CSV output line, or <see langword="null"/> on failure.
    /// </summary>
    internal static string? RunNvidiaSmi() =>
        ShellExtensions.RunProcess("nvidia-smi",
            "--query-gpu=power.draw,temperature.gpu,utilization.gpu,utilization.memory,memory.used,memory.total --format=csv,noheader,nounits");
}
