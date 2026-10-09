using CasCap;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Extension methods for configuring Haus services.</summary>
public static class HausServiceCollectionExtensions
{

    /// <summary>
    /// Registers <see cref="MediaStreamSinkService"/> and its configuration dependencies
    /// (<see cref="SecurityAgentConfig"/>, <see cref="MediaConfig"/>).
    /// Safe to call multiple times; uses <see cref="ServiceCollectionDescriptorExtensions.TryAddSingleton{TService,TImplementation}"/>
    /// to avoid duplicate registrations.
    /// </summary>
    /// <remarks>
    /// Required by any feature whose Haus-assembly sink depends on
    /// <see cref="IEventSink{T}"/> of <see cref="MediaEvent"/> (e.g. <see cref="DoorBirdSinkMediaStreamService"/>).
    /// </remarks>
    public static void AddMediaStreamSink(this IServiceCollection services)
    {
        services.AddCasCapConfiguration<SecurityAgentConfig>();
        services.AddCasCapConfiguration<MediaConfig>();
        services.TryAddSingleton<IEventSink<MediaEvent>, MediaStreamSinkService>();
    }

    /// <summary>Registers bounded MediaMTX event-clip capture for camera features.</summary>
    /// <param name="services">Service collection.</param>
    /// <param name="runWorker">Whether this process owns clip queue processing.</param>
    public static void AddCameraClipCapture(
        this IServiceCollection services,
        bool runWorker = true)
    {
        if (!services.Any(descriptor => descriptor.ServiceType == typeof(CameraClipQueue)))
        {
            services.AddCasCapConfiguration<CameraClipConfig>();
            services.TryAddSingleton<CameraClipQueue>();
            services.TryAddSingleton<CameraThumbnailPublisher>();
            services.AddHttpClient(nameof(CameraClipBgService), (sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<CameraClipConfig>>().Value;
                client.BaseAddress = new Uri(options.PlaybackBaseAddress);
                client.Timeout = Timeout.InfiniteTimeSpan;
            });
        }

        if (runWorker)
        {
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IHostedService, CameraClipBgService>());
        }
    }

    /// <summary>
    /// Registers the shared Signalizr communications pipeline with the SmartHaus comms agent, the
    /// media stream consumer, and the <see cref="CommunicationsBgService"/> and <see cref="MediaBgService"/>
    /// background workers for <c>Comms</c> deployments.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="lite">
    /// When <see langword="true"/>, registers only the messaging and agent
    /// dependencies without the <see cref="IBgFeature"/> background services.
    /// </param>
    public static void AddComms(this WebApplicationBuilder builder, bool lite = false)
    {
        builder.Services.AddCasCapConfiguration<HeatingAgentConfig>();
        builder.Services.AddCasCapConfiguration<AgentRuntimeAzureAuthConfig>();
        builder.Services.AddMediaStreamSink();
        builder.Services.AddComms(builder.Configuration, lite);
        var agentRuntimeClient = builder.Services.AddCommsAgent();
        var runtimeAuthConfig = builder.Configuration
            .GetSection(AgentRuntimeAzureAuthConfig.ConfigurationSectionName)
            .Get<AgentRuntimeAzureAuthConfig>() ?? new AgentRuntimeAzureAuthConfig();
        if (runtimeAuthConfig.Enabled)
        {
            builder.Services.AddTransient(serviceProvider =>
            {
                var authOptions = serviceProvider.GetRequiredService<IOptions<AgentRuntimeAzureAuthConfig>>();
                return new TokenCredentialBearerHandler(
                    authOptions.Value.TokenCredential,
                    authOptions.Value.Scope!);
            });
            agentRuntimeClient.AddHttpMessageHandler<TokenCredentialBearerHandler>();
        }
        builder.Services.AddSingleton<IAgentRunEnricher, EdgeHardwareAgentRunEnricher>();

        if (!lite)
            builder.Services.AddSingleton<IBgFeature, MediaBgService>();
    }

    /// <summary>
    /// Registers the hub-side <see cref="HubEvent"/> event sinks
    /// (<c>Console</c> and <c>Metrics</c>) enabled in <see cref="SignalRHubConfig.Sinks"/>
    /// and returns the loaded config for use in hub route registration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="lite">
    /// When <see langword="true"/>, registers only the event sinks
    /// without the <see cref="IFeature{T}"/> background service.
    /// </param>
    /// <param name="configure">Optional delegate to programmatically override configuration values.</param>
    /// <returns>The loaded <see cref="SignalRHubConfig"/> for use in hub route registration.</returns>
    public static SignalRHubConfig AddSignalRHub(this IServiceCollection services, IConfiguration configuration, bool lite = false,
        Action<SignalRHubConfig>? configure = null)
    {
        var config = services.AddAndGetCasCapConfiguration<SignalRHubConfig>(configuration, configure);

        services.AddEventSinks<HubEvent>(config.Sinks, typeof(HausHub).Assembly);

        if (!lite)
            services.AddSingleton<IBgFeature, HausHubSinksBgService>();

        return config;
    }

    //TODO: Ubiquiti network integration
    //TODO: Wiz lighting integration
}
