namespace CasCap.Extensions;

/// <summary>
/// Extension methods for registering Buderus sinks from the <c>CasCap.Api.Buderus.Sinks</c> assembly.
/// </summary>
public static class BuderusSinksServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Buderus services via <see cref="ServiceCollectionExtensions.AddBuderus"/> and then
    /// scans this assembly for additional <see cref="IEventSink{T}"/> implementations (Redis, Azure Tables, etc.)
    /// that replace the default in-memory sink when enabled.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="lite">
    /// When <see langword="true"/>, registers only the query service and its minimal
    /// dependencies without background services, health checks, or <see cref="IFeature{T}"/> implementations.
    /// </param>
    /// <param name="configure">Optional delegate to programmatically override configuration values.</param>
    /// <param name="additionalSinkAssemblies">Additional assemblies to scan for <see cref="IEventSink{T}"/> implementations.</param>
    public static void AddBuderusWithExtraSinks(this IServiceCollection services, IConfiguration configuration, bool lite = false,
        Action<BuderusConfig>? configure = null,
        params Assembly[] additionalSinkAssemblies)
    {
        services.AddBuderus(configuration, lite, configure);

        var config = configuration.GetCasCapConfiguration<BuderusConfig>();
        services.AddEventSinks<BuderusEvent>(lite ? config.Sinks.WithoutSinkType("Redis") : config.Sinks,
            [typeof(BuderusSinksServiceCollectionExtensions).Assembly, ..additionalSinkAssemblies]);
    }
}
