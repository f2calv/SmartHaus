namespace CasCap.Extensions;

/// <summary>
/// Extension methods for registering DoorBird services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers DoorBird services, event sinks, and dependencies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="lite">
    /// When <see langword="true"/>, registers only the query service and its minimal
    /// dependencies (options, sinks, HTTP client) without background services,
    /// health checks, or <see cref="IFeature{T}"/> implementations that require
    /// heavy infrastructure such as RedLock.
    /// </param>
    /// <param name="configure">Optional delegate to programmatically override configuration values.</param>
    public static void AddDoorBird(this IServiceCollection services, IConfiguration configuration, bool lite = false,
        Action<DoorBirdConfig>? configure = null)
    {
        var config = services.AddAndGetCasCapConfiguration<DoorBirdConfig>(configuration, configure);

        //named HttpClient is shared between the client and the healthcheck
        services.AddHttpClient(nameof(DoorBirdConnectionHealthCheck), (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<DoorBirdConfig>>().Value;
            client.BaseAddress = new Uri(opts.BaseAddress);
            client.SetBasicAuth(opts.Username, opts.Password);
        })
        .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
        .AddStandardResilience(nameof(DoorBirdConnectionHealthCheck))
        ;

        // Auto-register all sinks decorated with [SinkType] whose type is enabled
        services.AddEventSinks<DoorBirdEvent>(config.Sinks, typeof(ServiceCollectionExtensions).Assembly);

        services.AddSingleton<DoorBirdClientService>();

        if (!lite)
        {
            services.AddSingleton<DoorBirdConnectionHealthCheck>();
            services.AddSingleton<IBgFeature, DoorBirdBgService>();

            if (config.HealthCheck != KubernetesProbeTypes.None)
                services.AddHealthChecks()
                    .AddCheck<DoorBirdConnectionHealthCheck>(nameof(DoorBirdConnectionHealthCheck), tags: config.HealthCheck.GetTags());
        }

        services.AddSingleton<DoorBirdQueryService>();
        services.AddSingleton<IDoorBirdQueryService>(sp => sp.GetRequiredService<DoorBirdQueryService>());
    }
}
