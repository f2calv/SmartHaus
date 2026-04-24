namespace CasCap.Extensions;

/// <summary>Extension methods for configuring Miele services.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Adds Miele services to the service collection.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="configure">Optional configuration action.</param>
    public static void AddMiele(this IServiceCollection services, IConfiguration configuration,
        Action<MieleConfig>? configure = null)
    {
        var config = services.AddAndGetCasCapConfiguration<MieleConfig>(configuration, configure);

        services.AddHttpClient(nameof(MieleConnectionHealthCheck), (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<MieleConfig>>().Value;
            client.BaseAddress = new Uri(opts.HealthCheckUri);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", opts.OAuthToken);
        })
        .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
        .AddStandardResilience(nameof(MieleConnectionHealthCheck))
        ;

        services.AddSingleton<MieleClientService>();
        services.AddSingleton<IBgFeature, MieleEventStreamBgService>();

        services.AddSingleton<MieleConnectionHealthCheck>();

        if (config.HealthCheck != KubernetesProbeTypes.None)
            services.AddHealthChecks()
                .AddCheck<MieleConnectionHealthCheck>(nameof(MieleConnectionHealthCheck), tags: config.HealthCheck.GetTags());

        services.AddSingleton<MieleQueryService>();
        services.AddSingleton<IMieleQueryService>(sp => sp.GetRequiredService<MieleQueryService>());
    }
}
