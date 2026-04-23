using System.Reflection;

namespace CasCap.Extensions;

/// <summary>
/// Extension methods for registering Buderus KM200 services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Buderus KM200 services, event sinks, and health checks.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="lite">
    /// When <see langword="true"/>, registers only the query service and its minimal
    /// dependencies (options, sinks, HTTP client) without background services,
    /// health checks, or <see cref="IFeature{T}"/> implementations.
    /// </param>
    /// <param name="configure">Optional delegate to programmatically override configuration values.</param>
    public static void AddBuderus(this IServiceCollection services, IConfiguration configuration, bool lite = false,
        Action<BuderusConfig>? configure = null)
    {
        var config = services.AddAndGetCasCapConfiguration<BuderusConfig>(configuration, configure);

        //named HttpClient is shared between the client and the healthcheck
        services.AddHttpClient(nameof(BuderusKm200ConnectionHealthCheck), (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<BuderusConfig>>().Value;
            client.BaseAddress = new Uri($"{opts.BaseAddress}:{opts.Port}");
            client.DefaultRequestHeaders.Add("User-Agent", "TeleHeater/2.2.3");
        })
        .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
        .AddStandardResilience(nameof(BuderusKm200ConnectionHealthCheck))
        ;

        // Auto-register all sinks decorated with [SinkType] whose type is enabled
        var registeredSinks = services.AddEventSinks<BuderusEvent>(config.Sinks, typeof(ServiceCollectionExtensions).Assembly);

        // Ensure a Primary keyed sink is always available so BuderusQueryService can
        // resolve its [FromKeyedServices("Primary")] dependency. If no registered sink
        // implements IBuderusQuery (which triggers the Primary registration inside
        // AddEventSinks), fall back to the lightweight in-memory sink.
        // Hosts that later call AddBuderusWithExtraSinks will register a richer sink
        // (e.g. AzureTables) under the same key — the last keyed registration wins.
        if (!registeredSinks.Exists(t => typeof(IBuderusQuery).IsAssignableFrom(t)))
        {
            services.AddKeyedSingleton<IEventSink<BuderusEvent>, BuderusSinkMemoryService>(
                SinkServiceCollectionExtensions.PrimarySinkKey);
            services.AddSingleton<IEventSink<BuderusEvent>>(sp =>
                sp.GetRequiredKeyedService<IEventSink<BuderusEvent>>(SinkServiceCollectionExtensions.PrimarySinkKey));
            services.AddSingleton<IBuderusQuery>(sp =>
                (IBuderusQuery)sp.GetRequiredKeyedService<IEventSink<BuderusEvent>>(SinkServiceCollectionExtensions.PrimarySinkKey));
        }

        services.AddSingleton<BuderusKm200ClientService>();

        if (!lite)
        {
            if (config.HealthCheck != KubernetesProbeTypes.None)
            {
                services.AddSingleton<BuderusKm200ConnectionHealthCheck>();
                services.AddHealthChecks()
                    .AddCheck<BuderusKm200ConnectionHealthCheck>(nameof(BuderusKm200ConnectionHealthCheck), tags: config.HealthCheck.GetTags());
            }

            services.AddSingleton<IBgFeature, BuderusKm200MonitorBgService>();
        }

        services.AddSingleton<BuderusQueryService>();
        services.AddSingleton<IBuderusQueryService>(sp => sp.GetRequiredService<BuderusQueryService>());
    }
}
