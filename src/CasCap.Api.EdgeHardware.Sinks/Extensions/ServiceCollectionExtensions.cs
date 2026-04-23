namespace CasCap.Extensions;

/// <summary>
/// Extension methods for registering edge hardware sinks from the <c>CasCap.Api.EdgeHardware.Sinks</c> assembly.
/// </summary>
public static class EdgeHardwareSinksServiceCollectionExtensions
{
    /// <summary>
    /// Registers all edge hardware services via <see cref="EdgeHardwareServiceCollectionExtensions.AddEdgeHardware"/>
    /// and then scans this assembly for additional <see cref="IEventSink{T}"/> implementations
    /// (Redis, Azure Tables, etc.) that replace the default in-memory sink when enabled.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="lite">
    /// When <see langword="true"/>, registers only the query service and its minimal
    /// dependencies without background services, health checks, or CPU/GPU detection.
    /// </param>
    /// <param name="cpuEnabled">Whether CPU monitoring is enabled. Ignored when <paramref name="lite"/> is <see langword="true"/>.</param>
    /// <param name="additionalSinkAssemblies">Additional assemblies to scan for <see cref="IEventSink{T}"/> implementations.</param>
    /// <returns><see langword="true"/> if an NVIDIA GPU was detected; otherwise <see langword="false"/>.</returns>
    public static bool AddEdgeHardwareWithExtraSinks(this IServiceCollection services, IConfiguration configuration,
        bool lite = false, bool cpuEnabled = false,
        params Assembly[] additionalSinkAssemblies)
    {
        var gpuEnabled = services.AddEdgeHardware(configuration, lite, cpuEnabled);

        var config = configuration.GetSection(EdgeHardwareConfig.ConfigurationSectionName).Get<EdgeHardwareConfig>();
        if (config?.Sinks is not null)
        {
            services.AddEventSinks<EdgeHardwareEvent>(lite ? config.Sinks.WithoutSinkType("AzureTables") : config.Sinks,
                [typeof(EdgeHardwareSinksServiceCollectionExtensions).Assembly, ..additionalSinkAssemblies]);
        }

        return gpuEnabled;
    }
}
