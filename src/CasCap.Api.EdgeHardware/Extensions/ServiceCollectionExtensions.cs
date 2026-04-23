using System.Runtime.InteropServices;

namespace CasCap.Extensions;

/// <summary>
/// Extension methods for registering edge hardware monitoring services.
/// </summary>
public static class EdgeHardwareServiceCollectionExtensions
{
    /// <summary>
    /// Registers the unified edge hardware monitor, event sinks, and <see cref="IEdgeHardwareQueryService"/>.
    /// GPU availability is auto-detected via ILGPU; CPU temperature via <see cref="ICpuTemperatureProvider"/>
    /// — <see cref="CpuTemperatureService"/> on Linux,
    /// LibreHardwareCpuTemperatureService on Windows (conditional on WINDOWS).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="lite">
    /// When <see langword="true"/>, registers only the query service and its minimal
    /// dependencies (options, sinks) without background services, health checks, CPU
    /// temperature providers, or GPU detection.
    /// </param>
    /// <param name="cpuEnabled">Whether CPU monitoring is enabled. Ignored when <paramref name="lite"/> is <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when an NVIDIA GPU was detected; otherwise <see langword="false"/>.</returns>
    public static bool AddEdgeHardware(this IServiceCollection services, IConfiguration configuration,
        bool lite = false, bool cpuEnabled = false)
    {
        var config = services.AddAndGetCasCapConfiguration<EdgeHardwareConfig>(configuration);

        // Lightweight projection of the AppConfig section for Kubernetes node identity (used by metrics sink)
        services.AddCasCapConfiguration<KubeAppConfig>();

        // Auto-register all sinks decorated with [SinkType] whose type is enabled
        var registeredSinks = services.AddEventSinks<EdgeHardwareEvent>(config.Sinks, typeof(EdgeHardwareServiceCollectionExtensions).Assembly);

        // Ensure a Primary keyed sink is always available so EdgeHardwareQueryService can
        // resolve its [FromKeyedServices("Primary")] dependency. If no registered sink
        // implements IEdgeHardwareQuery (which triggers the Primary registration inside
        // AddEventSinks), fall back to the lightweight in-memory sink.
        // Hosts that later call AddEdgeHardwareWithExtraSinks will register a richer sink
        // (e.g. AzureTables) under the same key — the last keyed registration wins.
        if (!registeredSinks.Exists(t => typeof(IEdgeHardwareQuery).IsAssignableFrom(t)))
        {
            services.AddKeyedSingleton<IEventSink<EdgeHardwareEvent>, EdgeHardwareSinkMemoryService>(
                SinkServiceCollectionExtensions.PrimarySinkKey);
            services.AddSingleton<IEventSink<EdgeHardwareEvent>>(sp =>
                sp.GetRequiredKeyedService<IEventSink<EdgeHardwareEvent>>(SinkServiceCollectionExtensions.PrimarySinkKey));
            services.AddSingleton<IEdgeHardwareQuery>(sp =>
                (IEdgeHardwareQuery)sp.GetRequiredKeyedService<IEventSink<EdgeHardwareEvent>>(SinkServiceCollectionExtensions.PrimarySinkKey));
        }

        var gpuEnabled = false;

        if (!lite)
        {
            // CPU temperature provider — IoT bindings on Linux, LibreHardwareMonitor on Windows
            if (cpuEnabled)
            {
#if WINDOWS
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    services.AddSingleton<ICpuTemperatureProvider, LibreHardwareCpuTemperatureService>();
                else
#endif
                    services.AddSingleton<ICpuTemperatureProvider, CpuTemperatureService>();
            }

            // Auto-detect NVIDIA GPU via ILGPU
            gpuEnabled = DetectNvidiaGpu();

            // Unified monitor background service
            services.AddSingleton<EdgeHardwareMonitorBgService>();
            services.AddSingleton<IBgFeature>(sp => sp.GetRequiredService<EdgeHardwareMonitorBgService>());

            // Windows-only: one-shot ILGPU diagnostic service
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                services.AddSingleton<IBgFeature, GpuTestBgService>();
        }

        services.AddSingleton<EdgeHardwareQueryService>();
        services.AddSingleton<IEdgeHardwareQueryService>(sp => sp.GetRequiredService<EdgeHardwareQueryService>());

        return gpuEnabled;
    }

    /// <summary>
    /// Registers Raspberry Pi GPIO sensor services: <see cref="GpioMonitorBgService"/>,
    /// <see cref="GpioBmp280SensorService"/> and <see cref="GpioYfS201SensorService"/>.
    /// Also binds <see cref="EdgeHardwareConfig"/>.
    /// </summary>
    /// <remarks>Skips registration on non-Linux platforms where GPIO bindings are unavailable.</remarks>
    public static void AddEdgeHardwarePi(this IServiceCollection services, IConfiguration configuration,
        Action<EdgeHardwareConfig>? configure = null)
    {
        services.AddCasCapConfiguration<EdgeHardwareConfig>(configure);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        services.AddSingleton<GpioBmp280SensorService>();
        services.AddSingleton<GpioYfS201SensorService>();

        services.AddSingleton<IBgFeature, GpioMonitorBgService>();
    }

    /// <summary>Probes ILGPU for a non-CPU accelerator (i.e. an NVIDIA GPU).</summary>
    private static bool DetectNvidiaGpu()
    {
        try
        {
            using var context = ILGPU.Context.CreateDefault();
            foreach (var device in context)
            {
                if (device.AcceleratorType != ILGPU.Runtime.AcceleratorType.CPU)
                    return true;
            }
        }
        catch
        {
            // ILGPU init can fail on machines without compatible drivers — treat as no GPU.
        }
        return false;
    }
}
