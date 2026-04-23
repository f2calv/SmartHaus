namespace CasCap.Extensions;

/// <summary>Extension methods for registering Wiz smart lighting sinks.</summary>
public static class WizSinksServiceCollectionExtensions
{
    /// <summary>Registers core Wiz services plus all configured event sinks.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="lite">When <see langword="true"/>, excludes Redis sinks.</param>
    /// <param name="configure">Optional delegate to programmatically override configuration values.</param>
    /// <param name="additionalSinkAssemblies">Additional assemblies to scan for <see cref="IEventSink{T}"/> implementations.</param>
    public static void AddWizWithExtraSinks(this IServiceCollection services, IConfiguration configuration,
        bool lite = false, Action<WizConfig>? configure = null,
        params Assembly[] additionalSinkAssemblies)
    {
        services.AddWiz(configuration, configure);
        var config = configuration.GetCasCapConfiguration<WizConfig>();
        services.AddEventSinks<WizEvent>(lite ? config.Sinks.WithoutSinkType("Redis") : config.Sinks,
            [typeof(WizSinksServiceCollectionExtensions).Assembly, ..additionalSinkAssemblies]);
    }
}
