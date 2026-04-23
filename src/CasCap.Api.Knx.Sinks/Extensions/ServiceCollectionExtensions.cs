namespace CasCap.Extensions;

/// <summary>
/// Extension methods for registering KNX sinks from the <c>CasCap.Api.Knx.Sinks</c> assembly.
/// </summary>
public static class KnxSinksServiceCollectionExtensions
{
    /// <summary>
    /// Registers all KNX services via <see cref="ServiceCollectionExtensions.AddKnx"/> and then
    /// scans this assembly for additional <see cref="IEventSink{T}"/> implementations (Redis, Azure Tables, etc.)
    /// that replace the default in-memory sink when enabled.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="lite">
    /// When <see langword="true"/>, registers only the query service and its minimal
    /// dependencies without background services, health checks, or <see cref="IFeature{T}"/> implementations.
    /// Forces <see cref="KnxMemoryStateService"/> regardless of Redis configuration.
    /// </param>
    /// <param name="configure">Optional delegate to programmatically override configuration values.</param>
    /// <param name="additionalSinkAssemblies">Additional assemblies to scan for <see cref="IEventSink{T}"/> implementations.</param>
    public static void AddKnxWithExtraSinks(this IServiceCollection services, IConfiguration configuration, bool lite = false,
        Action<KnxConfig>? configure = null,
        params Assembly[] additionalSinkAssemblies)
    {
        services.AddKnx(configuration, lite, configure);

        var config = configuration.GetCasCapConfiguration<KnxConfig>();
        services.AddEventSinks<KnxEvent>(lite ? config.Sinks.WithoutSinkType("Redis") : config.Sinks,
            [typeof(KnxSinksServiceCollectionExtensions).Assembly, ..additionalSinkAssemblies]);
    }
}
