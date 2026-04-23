namespace CasCap.Extensions;

/// <summary>
/// Extension methods for registering Ubiquiti services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Ubiquiti UniFi Protect services, event sinks, and dependencies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="lite">
    /// When <see langword="true"/>, registers only the query service and its minimal
    /// dependencies (options, sinks) without the idle background service.
    /// </param>
    /// <param name="configure">Optional delegate to programmatically override configuration values.</param>
    public static void AddUbiquiti(this IServiceCollection services, IConfiguration configuration, bool lite = false,
        Action<UbiquitiConfig>? configure = null)
    {
        var config = services.AddAndGetCasCapConfiguration<UbiquitiConfig>(configuration, configure);

        // Auto-register all sinks decorated with [SinkType] whose type is enabled
        services.AddEventSinks<UbiquitiEvent>(config.Sinks, typeof(ServiceCollectionExtensions).Assembly);

        if (!lite)
            services.AddSingleton<IBgFeature, UbiquitiBgService>();

        services.AddSingleton<UbiquitiQueryService>();
        services.AddSingleton<IUbiquitiQueryService>(sp => sp.GetRequiredService<UbiquitiQueryService>());
    }
}
