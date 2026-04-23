namespace CasCap.Extensions;

/// <summary>Extension methods for registering Miele appliance sinks.</summary>
public static class MieleSinksServiceCollectionExtensions
{
    /// <summary>Registers core Miele services plus all configured event sinks.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="lite">When <see langword="true"/>, excludes Redis sinks.</param>
    /// <param name="configure">Optional delegate to programmatically override configuration values.</param>
    /// <param name="additionalSinkAssemblies">Additional assemblies to scan for <see cref="IEventSink{T}"/> implementations.</param>
    public static void AddMieleWithExtraSinks(this IServiceCollection services, IConfiguration configuration,
        bool lite = false, Action<MieleConfig>? configure = null,
        params Assembly[] additionalSinkAssemblies)
    {
        services.AddMiele(configuration, configure);
        var config = configuration.GetCasCapConfiguration<MieleConfig>();
        services.AddEventSinks<MieleEvent>(lite ? config.Sinks.WithoutSinkType("Redis") : config.Sinks,
            [typeof(MieleSinksServiceCollectionExtensions).Assembly, ..additionalSinkAssemblies]);
    }
}
