namespace CasCap.Extensions;

/// <summary>
/// Extension methods for registering DoorBird sinks from the <c>CasCap.Api.DoorBird.Sinks</c> assembly.
/// </summary>
public static class DoorBirdSinksServiceCollectionExtensions
{
    /// <summary>
    /// Registers all DoorBird services via <see cref="ServiceCollectionExtensions.AddDoorBird"/> and then
    /// scans this assembly (and any <paramref name="additionalSinkAssemblies"/>) for additional
    /// <see cref="IEventSink{T}"/> implementations (Redis, Azure Tables, etc.)
    /// that replace the default in-memory sink when enabled.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="lite">
    /// When <see langword="true"/>, registers only the query service and its minimal
    /// dependencies without background services, health checks, or <see cref="IFeature{T}"/> implementations
    /// that require heavy infrastructure such as RedLock.
    /// </param>
    /// <param name="configure">Optional delegate to programmatically override configuration values.</param>
    /// <param name="tokenCredential">
    /// Azure token credential for authenticating to Blob Storage. When supplied the
    /// <see cref="DoorBirdConfig.AzureBlobStorageConnectionString"/> is treated as an endpoint URI;
    /// when <see langword="null"/> it is used as a full connection string.
    /// </param>
    /// <param name="additionalSinkAssemblies">Additional assemblies to scan for <see cref="IEventSink{T}"/> implementations (e.g. SignalCli).</param>
    public static void AddDoorBirdWithExtraSinks(this IServiceCollection services, IConfiguration configuration,
        bool lite = false,
        Action<DoorBirdConfig>? configure = null,
        TokenCredential? tokenCredential = null,
        params Assembly[] additionalSinkAssemblies)
    {
        services.AddDoorBird(configuration, lite, configure);

        var config = configuration.GetCasCapConfiguration<DoorBirdConfig>();

        if (!lite)
        {
            if (config.AzureBlobStorageConnectionString.Contains(';'))
                services.AddSingleton<IDoorBirdAzBlobStorageService>(
                    new DoorBirdAzBlobStorageService(config.AzureBlobStorageConnectionString, config.AzureBlobStorageContainerName));
            else
                services.AddSingleton<IDoorBirdAzBlobStorageService>(
                    new DoorBirdAzBlobStorageService(new Uri(config.AzureBlobStorageConnectionString), config.AzureBlobStorageContainerName, tokenCredential!));
            services.AddHostedService<BlobProcessorBgService>();
        }

        services.AddEventSinks<DoorBirdEvent>(lite ? config.Sinks.WithoutSinkType("Redis") : config.Sinks,
            [typeof(DoorBirdSinksServiceCollectionExtensions).Assembly, ..additionalSinkAssemblies]);
    }
}
