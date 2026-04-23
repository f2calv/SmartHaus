namespace CasCap.Extensions;

/// <summary>Extension methods for configuring Sicce services.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Sicce services, event sinks, and health checks.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="lite">
    /// When <see langword="true"/>, registers only the query service and its minimal
    /// dependencies (options, sinks, HTTP client) without background services,
    /// health checks, or <see cref="IFeature{T}"/> implementations.
    /// </param>
    /// <param name="configure">Optional delegate to programmatically override configuration values.</param>
    public static void AddSicce(this IServiceCollection services, IConfiguration configuration, bool lite = false,
        Action<SicceConfig>? configure = null)
    {
        var config = services.AddAndGetCasCapConfiguration<SicceConfig>(configuration, configure);

        //named HttpClient is shared between the client and the healthcheck
        services.AddHttpClient(nameof(SicceConnectionHealthCheck), (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<SicceConfig>>().Value;
            client.BaseAddress = new Uri(opts.BaseAddress);
            client.DefaultRequestHeaders.Add("Vendor-Key", opts.VendorKey);
        })
        .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
        .AddStandardResilience(nameof(SicceConnectionHealthCheck))
        ;

        services.AddSingleton<SicceClientService>();

        // Auto-register all sinks decorated with [SinkType] whose type is enabled
        var registeredSinks = services.AddEventSinks<SicceEvent>(config.Sinks, typeof(ServiceCollectionExtensions).Assembly);

        // Ensure a Primary keyed sink is always available so SicceQueryService can
        // resolve its [FromKeyedServices("Primary")] dependency. If no registered sink
        // implements ISicceQuery (which triggers the Primary registration inside
        // AddEventSinks), fall back to the lightweight in-memory sink.
        // Hosts that later call AddSicceWithExtraSinks will register a richer sink
        // (e.g. AzureTables) under the same key — the last keyed registration wins.
        if (!registeredSinks.Exists(t => typeof(ISicceQuery).IsAssignableFrom(t)))
        {
            services.AddKeyedSingleton<IEventSink<SicceEvent>, SicceSinkMemoryService>(
                SinkServiceCollectionExtensions.PrimarySinkKey);
            services.AddSingleton<IEventSink<SicceEvent>>(sp =>
                sp.GetRequiredKeyedService<IEventSink<SicceEvent>>(SinkServiceCollectionExtensions.PrimarySinkKey));
            services.AddSingleton<ISicceQuery>(sp =>
                (ISicceQuery)sp.GetRequiredKeyedService<IEventSink<SicceEvent>>(SinkServiceCollectionExtensions.PrimarySinkKey));
        }

        if (!lite)
        {
            if (config.HealthCheck != KubernetesProbeTypes.None)
            {
                services.AddSingleton<SicceConnectionHealthCheck>();
                services.AddHealthChecks()
                    .AddCheck<SicceConnectionHealthCheck>(nameof(SicceConnectionHealthCheck), tags: config.HealthCheck.GetTags());
            }
            services.AddSingleton<IBgFeature, SicceBgService>();
        }

        services.AddSingleton<SicceQueryService>();
        services.AddSingleton<ISicceQueryService>(sp => sp.GetRequiredService<SicceQueryService>());
    }
}
