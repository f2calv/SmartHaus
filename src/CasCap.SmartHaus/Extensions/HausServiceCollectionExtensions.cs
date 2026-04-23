using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.AI;
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

    /// <summary>
    /// Registers the signal-cli REST API, comms stream sink, media stream consumer,
    /// and the <see cref="CommunicationsBgService"/> and <see cref="MediaBgService"/>
    /// background workers for <c>Comms</c> deployments.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="lite">
    /// When <see langword="true"/>, registers only the messaging and agent
    /// dependencies without the <see cref="IBgFeature"/> background services.
    /// </param>
    public static void AddComms(this WebApplicationBuilder builder, bool lite = false)
    {
        builder.Services.AddCasCapConfiguration<CommsAgentConfig>();
        builder.Services.AddCasCapConfiguration<HeatingAgentConfig>();
        builder.Services.AddMediaStreamSink();
        builder.Services.AddSignalCli(builder.Configuration, builder.Environment.IsDevelopment());
        builder.Services.AddMessagingMcp(
            builder.Configuration[$"{SignalCliConfig.ConfigurationSectionName}:{nameof(SignalCliConfig.PhoneNumber)}"]!,
            builder.Configuration[$"{CommsAgentConfig.ConfigurationSectionName}:{nameof(CommsAgentConfig.GroupName)}"]!);
        builder.Services.AddSingleton<DistributedCacheSessionStore>();
        builder.Services.AddSingleton<ISessionStore>(sp => sp.GetRequiredService<DistributedCacheSessionStore>());
        builder.Services.AddSingleton<AgentCommandHandler>();

        builder.Services.AddSingleton<CommsDebugNotifier>();

        if (!lite)
        {
            builder.Services.AddSingleton<IBgFeature, CommunicationsBgService>();
            builder.Services.AddSingleton<IBgFeature, MediaBgService>();
        }
    }

    /// <summary>
    /// Creates an <see cref="AIAgent"/> from the specified <see cref="ProviderConfig"/> and <see cref="AgentConfig"/>.
    /// Delegates to <see cref="AgentExtensions.CreateAgent"/> for the core building logic,
    /// adding development-mode authentication resolved from <see cref="IOptions{ApiAuthConfig}"/>.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="provider">The infrastructure provider (connection, model, auth).</param>
    /// <param name="agentConfig">The agent behavioral configuration.</param>
    /// <param name="serviceProvider">The service provider to resolve <see cref="ApiAuthConfig"/> from.</param>
    /// <param name="tools">Optional list of AI tools to register with the agent.</param>
    /// <param name="otelSourceName">
    /// Optional OpenTelemetry activity source name. Use <see cref="AgentExtensions.GetAISourceName"/> to derive
    /// from <see cref="AppConfig.MetricNamePrefix"/>.
    /// </param>
    /// <param name="aiConfig">
    /// Optional root AI configuration supplying shared <see cref="AIConfig.InstructionsPrefix"/>
    /// and <see cref="AIConfig.InstructionsSuffix"/>.
    /// </param>
    public static (IChatClient chatClient, AIAgent agent, string instructions) CreateAgent(this WebApplicationBuilder builder,
        ProviderConfig provider, AgentConfig agentConfig, IServiceProvider serviceProvider, List<AITool>? tools = null, string? otelSourceName = null, AIConfig? aiConfig = null)
    {
        // Infrastructure auth (k8s ingress basic auth) is only needed for Ollama
        // when running outside the cluster. OpenAI/AzureOpenAI auth is handled separately.
        HttpClient? httpClient = null;
        if (provider.Type is AgentType.Ollama)
        {
            httpClient = new HttpClient
            {
                BaseAddress = provider.Endpoint,
                Timeout = Timeout.InfiniteTimeSpan,
            };
            if (builder.Environment.IsDevelopment())
            {
                var authOpts = serviceProvider.GetRequiredService<IOptions<ApiAuthConfig>>().Value;
                httpClient.SetBasicAuth(authOpts.Username, authOpts.Password);
            }
        }

        // Resolve TokenCredential for Azure Entra ID-based providers (e.g. AzureOpenAI).
        var tokenCredential = provider.Type is AgentType.AzureOpenAI
            ? serviceProvider.GetRequiredService<IOptions<AppConfig>>().Value.TokenCredential
            : null;

        return AgentExtensions.CreateAgent(provider, agentConfig, httpClient, tools,
            instructionsAssembly: typeof(HausServiceCollectionExtensions).Assembly,
            aiConfig: aiConfig,
            otelSourceName: otelSourceName,
            tokenCredential: tokenCredential);
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
