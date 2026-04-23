namespace CasCap.Extensions;

/// <summary>
/// Extension methods for registering Ubiquiti sinks from the <c>CasCap.Api.Ubiquiti.Sinks</c> assembly.
/// </summary>
public static class UbiquitiSinksServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Ubiquiti services via <see cref="ServiceCollectionExtensions.AddUbiquiti"/> and then
    /// scans this assembly (and any <paramref name="additionalSinkAssemblies"/>) for additional
    /// <see cref="IEventSink{T}"/> implementations (Redis, Azure Tables, etc.)
    /// that replace the default in-memory sink when enabled.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="lite">
    /// When <see langword="true"/>, registers only the query service and its minimal
    /// dependencies without Redis sinks or <see cref="IFeature{T}"/> implementations
    /// that require heavy infrastructure such as RedLock.
    /// </param>
    /// <param name="configure">Optional delegate to programmatically override configuration values.</param>
    /// <param name="additionalSinkAssemblies">Additional assemblies to scan for <see cref="IEventSink{T}"/> implementations.</param>
    public static void AddUbiquitiWithExtraSinks(this IServiceCollection services, IConfiguration configuration,
        bool lite = false,
        Action<UbiquitiConfig>? configure = null,
        params Assembly[] additionalSinkAssemblies)
    {
        services.AddUbiquiti(configuration, lite, configure);

        var config = configuration.GetCasCapConfiguration<UbiquitiConfig>();

        services.AddEventSinks<UbiquitiEvent>(lite ? config.Sinks.WithoutSinkType("Redis") : config.Sinks,
            [typeof(UbiquitiSinksServiceCollectionExtensions).Assembly, ..additionalSinkAssemblies]);
    }
}
