namespace CasCap.Extensions;

/// <summary>
/// Extension methods for registering Fronius services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Fronius services, event sinks, and health checks.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="lite">
    /// When <see langword="true"/>, registers only the query service and its minimal
    /// dependencies (options, sinks, HTTP client) without background services,
    /// health checks, or <see cref="IFeature{T}"/> implementations.
    /// </param>
    /// <param name="configure">Optional delegate to programmatically override configuration values.</param>
    public static void AddFronius(this IServiceCollection services, IConfiguration configuration, bool lite = false,
        Action<FroniusConfig>? configure = null)
    {
        var config = services.AddAndGetCasCapConfiguration<FroniusConfig>(configuration, configure);

        //named HttpClient is shared between the client and the healthcheck
        services.AddHttpClient(nameof(FroniusSymoConnectionHealthCheck), (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<FroniusConfig>>().Value;
            client.BaseAddress = new Uri(opts.BaseAddress);
        })
        .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
        .AddStandardResilience(nameof(FroniusSymoConnectionHealthCheck))
        ;

        services.AddSingleton<FroniusClientService>();

        // Auto-register all sinks decorated with [SinkType] whose type is enabled
        var registeredSinks = services.AddEventSinks<FroniusEvent>(config.Sinks, typeof(ServiceCollectionExtensions).Assembly);

        // Ensure a Primary keyed sink is always available so FroniusQueryService can
        // resolve its [FromKeyedServices("Primary")] dependency. If no registered sink
        // implements IFroniusQuery (which triggers the Primary registration inside
        // AddEventSinks), fall back to the lightweight in-memory sink.
        // Hosts that later call AddFroniusWithExtraSinks will register a richer sink
        // (e.g. AzureTables) under the same key — the last keyed registration wins.
        if (!registeredSinks.Exists(t => typeof(IFroniusQuery).IsAssignableFrom(t)))
        {
            services.AddKeyedSingleton<IEventSink<FroniusEvent>, FroniusSinkMemoryService>(
                SinkServiceCollectionExtensions.PrimarySinkKey);
            services.AddSingleton<IEventSink<FroniusEvent>>(sp =>
                sp.GetRequiredKeyedService<IEventSink<FroniusEvent>>(SinkServiceCollectionExtensions.PrimarySinkKey));
            services.AddSingleton<IFroniusQuery>(sp =>
                (IFroniusQuery)sp.GetRequiredKeyedService<IEventSink<FroniusEvent>>(SinkServiceCollectionExtensions.PrimarySinkKey));
        }

        if (!lite)
        {
            services.AddSingleton<FroniusSymoConnectionHealthCheck>();

            if (config.HealthCheck != KubernetesProbeTypes.None)
                services.AddHealthChecks()
                    .AddCheck<FroniusSymoConnectionHealthCheck>(nameof(FroniusSymoConnectionHealthCheck), tags: config.HealthCheck.GetTags());

            services.AddSingleton<IBgFeature, FroniusMonitorBgService>();
        }

        services.AddSingleton<FroniusQueryService>();
        services.AddSingleton<IFroniusQueryService>(sp => sp.GetRequiredService<FroniusQueryService>());
    }
}
