namespace CasCap.Extensions;

/// <summary>
/// Extension methods for registering Shelly services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Shelly services, event sinks, and health checks.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="lite">
    /// When <see langword="true"/>, registers only the query service and its minimal
    /// dependencies (options, sinks, HTTP client) without background services,
    /// health checks, or <see cref="IFeature{T}"/> implementations.
    /// </param>
    /// <param name="configure">Optional delegate to programmatically override configuration values.</param>
    public static void AddShelly(this IServiceCollection services, IConfiguration configuration, bool lite = false,
        Action<ShellyConfig>? configure = null)
    {
        var config = services.AddAndGetCasCapConfiguration<ShellyConfig>(configuration, configure);

        //named HttpClient is shared between the client and the healthcheck
        services.AddHttpClient(nameof(ShellyCloudConnectionHealthCheck), (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<ShellyConfig>>().Value;
            client.BaseAddress = new Uri(opts.BaseAddress);
        })
        .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
        .AddStandardResilience(nameof(ShellyCloudConnectionHealthCheck))
        ;

        services.AddSingleton<ShellyCloudClientService>();

        // Auto-register all sinks decorated with [SinkType] whose type is enabled
        var registeredSinks = services.AddEventSinks<ShellyEvent>(config.Sinks, typeof(ServiceCollectionExtensions).Assembly);

        // Ensure a Primary keyed sink is always available so ShellyQueryService can
        // resolve its [FromKeyedServices("Primary")] dependency. If no registered sink
        // implements IShellyQuery (which triggers the Primary registration inside
        // AddEventSinks), fall back to the lightweight in-memory sink.
        if (!registeredSinks.Exists(t => typeof(IShellyQuery).IsAssignableFrom(t)))
        {
            services.AddKeyedSingleton<IEventSink<ShellyEvent>, ShellySinkMemoryService>(
                SinkServiceCollectionExtensions.PrimarySinkKey);
            services.AddSingleton<IEventSink<ShellyEvent>>(sp =>
                sp.GetRequiredKeyedService<IEventSink<ShellyEvent>>(SinkServiceCollectionExtensions.PrimarySinkKey));
            services.AddSingleton<IShellyQuery>(sp =>
                (IShellyQuery)sp.GetRequiredKeyedService<IEventSink<ShellyEvent>>(SinkServiceCollectionExtensions.PrimarySinkKey));
        }

        if (!lite)
        {
            services.AddSingleton<ShellyCloudConnectionHealthCheck>();

            if (config.HealthCheck != KubernetesProbeTypes.None)
                services.AddHealthChecks()
                    .AddCheck<ShellyCloudConnectionHealthCheck>(nameof(ShellyCloudConnectionHealthCheck), tags: config.HealthCheck.GetTags());

            services.AddSingleton<IBgFeature, ShellyMonitorBgService>();
        }

        services.AddSingleton<ShellyQueryService>();
        services.AddSingleton<IShellyQueryService>(sp => sp.GetRequiredService<ShellyQueryService>());
    }
}
