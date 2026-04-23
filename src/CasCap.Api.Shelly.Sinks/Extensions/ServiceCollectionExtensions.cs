namespace CasCap.Extensions;

/// <summary>
/// Extension methods for registering Shelly sinks from the <c>CasCap.Api.Shelly.Sinks</c> assembly.
/// </summary>
public static class ShellySinksServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Shelly services via <see cref="ServiceCollectionExtensions.AddShelly"/> and then
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
    public static void AddShellyWithExtraSinks(this IServiceCollection services, IConfiguration configuration, bool lite = false,
        Action<ShellyConfig>? configure = null,
        params Assembly[] additionalSinkAssemblies)
    {
        services.AddShelly(configuration, lite, configure);

        var config = configuration.GetCasCapConfiguration<ShellyConfig>();
        services.AddEventSinks<ShellyEvent>(lite ? config.Sinks.WithoutSinkType("Redis") : config.Sinks,
            [typeof(ShellySinksServiceCollectionExtensions).Assembly, ..additionalSinkAssemblies]);
    }
}
