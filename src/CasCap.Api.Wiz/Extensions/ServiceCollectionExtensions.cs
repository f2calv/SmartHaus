namespace CasCap.Extensions;

/// <summary>Extension methods for configuring Wiz smart lighting services.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Adds Wiz smart lighting services to the service collection.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="configure">Optional configuration action.</param>
    public static void AddWiz(this IServiceCollection services, IConfiguration configuration,
        Action<WizConfig>? configure = null)
    {
        var config = services.AddAndGetCasCapConfiguration<WizConfig>(configuration, configure);

        services.AddSingleton<WizClientService>();
        services.AddSingleton<WizQueryService>();
        services.AddSingleton<IWizQueryService>(sp => sp.GetRequiredService<WizQueryService>());
        services.AddSingleton<IBgFeature, WizDiscoveryBgService>();

        services.AddSingleton<WizConnectionHealthCheck>();

        if (config.HealthCheck != KubernetesProbeTypes.None)
            services.AddHealthChecks()
                .AddCheck<WizConnectionHealthCheck>(nameof(WizConnectionHealthCheck), tags: config.HealthCheck.GetTags());
    }
}
