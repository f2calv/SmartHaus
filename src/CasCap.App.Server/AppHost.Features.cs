using CasCap.Constants;
using ModelContextProtocol.AspNetCore;
using StackExchange.Redis;

namespace CasCap;

public static partial class AppHost
{
    private static SignalRHubConfig? AddFeatures(
        WebApplicationBuilder builder,
        AppConfig appConfig,
        AIConfig aiConfig,
        IReadOnlySet<string> enabledFeatures,
        IConnectionMultiplexer connectionMultiplexer)
    {
        var mcpBuilder = builder.Services.AddMcpServer()
            .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless);

        // SystemMcpQueryService is referenced by all agents - register unconditionally.
        builder.Services.AddSystemMcp();

        // Register SignalR services unconditionally so IHubContext<> is always resolvable for
        // HausHub sinks discovered during assembly scanning (e.g. BuderusSinkSignalRService).
        // The hub endpoint mapping and Redis backplane are configured later when SignalRHub is enabled.
        builder.Services.AddSignalR();

        if (enabledFeatures.Contains(FeatureNames.Buderus) || enabledFeatures.Contains(FeatureNames.Comms))
        {
            builder.Services.AddBuderusWithExtraSinks(builder.Configuration,
                lite: !enabledFeatures.Contains(FeatureNames.Buderus),
                additionalSinkAssemblies: [typeof(HausServiceCollectionExtensions).Assembly]);
            builder.Services.AddHeatPumpMcp();
            mcpBuilder.WithToolsFromAssembly(typeof(HeatPumpMcpQueryService).Assembly);
            mcpBuilder.WithPromptsFromAssembly(typeof(HeatPumpMcpQueryService).Assembly);
        }

        if (enabledFeatures.Contains(FeatureNames.Sicce))
            builder.Services.AddSicceWithExtraSinks(builder.Configuration,
                additionalSinkAssemblies: [typeof(HausServiceCollectionExtensions).Assembly]);

        if (enabledFeatures.Contains(FeatureNames.Wiz) || enabledFeatures.Contains(FeatureNames.Comms))
        {
            builder.Services.AddWizWithExtraSinks(builder.Configuration,
                lite: !enabledFeatures.Contains(FeatureNames.Wiz),
                additionalSinkAssemblies: [typeof(HausServiceCollectionExtensions).Assembly]);
        }

        if (enabledFeatures.Contains(FeatureNames.EdgeHardware))
            builder.Services.AddEdgeHardwarePi(builder.Configuration);

        if (enabledFeatures.Contains(FeatureNames.EdgeHardware) || enabledFeatures.Contains(FeatureNames.Comms))
        {
            builder.Services.AddEdgeHardwareWithExtraSinks(builder.Configuration,
                lite: !enabledFeatures.Contains(FeatureNames.EdgeHardware),
                cpuEnabled: enabledFeatures.Contains(FeatureNames.EdgeHardware),
                additionalSinkAssemblies: [typeof(HausServiceCollectionExtensions).Assembly]);
            builder.Services.AddEdgeHardwareMcp();
            mcpBuilder.WithToolsFromAssembly(typeof(EdgeHardwareMcpQueryService).Assembly);
            mcpBuilder.WithPromptsFromAssembly(typeof(EdgeHardwareMcpQueryService).Assembly);
        }

        if (enabledFeatures.Contains(FeatureNames.DoorBird) || enabledFeatures.Contains(FeatureNames.Comms))
        {
            builder.Services.AddMediaStreamSink();
            builder.Services.AddCameraClipCapture(
                runWorker: enabledFeatures.Contains(FeatureNames.DoorBird));
            builder.Services.AddDoorBirdWithExtraSinks(builder.Configuration,
                lite: !enabledFeatures.Contains(FeatureNames.DoorBird),
                tokenCredential: appConfig.TokenCredential,
                additionalSinkAssemblies: [typeof(HausServiceCollectionExtensions).Assembly]);
            builder.Services.AddFrontDoorMcp();
            mcpBuilder.WithToolsFromAssembly(typeof(FrontDoorMcpQueryService).Assembly);
            mcpBuilder.WithPromptsFromAssembly(typeof(FrontDoorMcpQueryService).Assembly);
        }

        if (enabledFeatures.Contains(FeatureNames.DDns))
            builder.Services.AddDDns(builder.Configuration);

        if (enabledFeatures.Contains(FeatureNames.Knx) || enabledFeatures.Contains(FeatureNames.Comms))
        {
            builder.Services.AddKnxWithExtraSinks(builder.Configuration,
                lite: !enabledFeatures.Contains(FeatureNames.Knx),
                additionalSinkAssemblies: [typeof(HausServiceCollectionExtensions).Assembly]);
            builder.Services.AddBusSystemMcp();
            mcpBuilder.WithToolsFromAssembly(typeof(BusSystemMcpQueryService).Assembly);
            mcpBuilder.WithPromptsFromAssembly(typeof(BusSystemMcpQueryService).Assembly);
        }

        if (enabledFeatures.Contains(FeatureNames.Fronius) || enabledFeatures.Contains(FeatureNames.Comms))
        {
            builder.Services.AddFroniusWithExtraSinks(builder.Configuration,
                lite: !enabledFeatures.Contains(FeatureNames.Fronius),
                additionalSinkAssemblies: [typeof(HausServiceCollectionExtensions).Assembly]);
            builder.Services.AddInverterMcp();
            mcpBuilder.WithToolsFromAssembly(typeof(InverterMcpQueryService).Assembly);
            mcpBuilder.WithPromptsFromAssembly(typeof(InverterMcpQueryService).Assembly);
        }

        if (enabledFeatures.Contains(FeatureNames.Shelly) || enabledFeatures.Contains(FeatureNames.Comms))
        {
            builder.Services.AddShellyWithExtraSinks(builder.Configuration,
                lite: !enabledFeatures.Contains(FeatureNames.Shelly),
                additionalSinkAssemblies: [typeof(HausServiceCollectionExtensions).Assembly]);
            builder.Services.AddSmartPlugMcp();
            mcpBuilder.WithToolsFromAssembly(typeof(SmartPlugMcpQueryService).Assembly);
            mcpBuilder.WithPromptsFromAssembly(typeof(SmartPlugMcpQueryService).Assembly);
        }

        // SmartLightingMcp spans Wiz, KNX and Shelly - all three DI parameters are nullable.
        if (enabledFeatures.Contains(FeatureNames.Wiz) || enabledFeatures.Contains(FeatureNames.Knx)
            || enabledFeatures.Contains(FeatureNames.Shelly) || enabledFeatures.Contains(FeatureNames.Comms))
        {
            builder.Services.AddSmartLightingMcp();
            mcpBuilder.WithToolsFromAssembly(typeof(SmartLightingMcpQueryService).Assembly);
            mcpBuilder.WithPromptsFromAssembly(typeof(SmartLightingMcpQueryService).Assembly);
        }

        if (enabledFeatures.Contains(FeatureNames.Ubiquiti))
        {
            builder.Services.AddMediaStreamSink();
            builder.Services.AddCameraClipCapture();
            builder.Services.AddUbiquitiWithExtraSinks(builder.Configuration,
                lite: !enabledFeatures.Contains(FeatureNames.Ubiquiti),
                additionalSinkAssemblies: [typeof(HausServiceCollectionExtensions).Assembly]);
            builder.Services.AddCamerasMcp();
            mcpBuilder.WithToolsFromAssembly(typeof(IpCameraMcpQueryService).Assembly);
            mcpBuilder.WithPromptsFromAssembly(typeof(IpCameraMcpQueryService).Assembly);
        }

        if (enabledFeatures.Contains(FeatureNames.Miele))
        {
            builder.Services.AddMieleWithExtraSinks(builder.Configuration,
                lite: !enabledFeatures.Contains(FeatureNames.Miele),
                additionalSinkAssemblies: [typeof(HausServiceCollectionExtensions).Assembly]);
            builder.Services.AddAppliancesMcp();
            mcpBuilder.WithToolsFromAssembly(typeof(AppliancesMcpQueryService).Assembly);
            mcpBuilder.WithPromptsFromAssembly(typeof(AppliancesMcpQueryService).Assembly);
        }

        if (enabledFeatures.Contains(FeatureNames.Comms))
        {
            builder.AddComms();
            mcpBuilder.WithToolsFromAssembly(typeof(MessagingMcpQueryService).Assembly);
        }

        // Index tool service and prompt types by name so agent config resolves them deterministically,
        // rather than scanning every loaded assembly. Must run after the AddXxxMcp registrations above.
        builder.Services.AddAgentTypeRegistry(typeof(SystemMcpQueryService).Assembly);

        // Register all AI agent profiles with deferred tool resolution.
        var otelSourceName = AgentExtensions.GetAISourceName(appConfig.MetricNamePrefix);
        foreach (var (agentName, agentConfig) in aiConfig.Agents.Where(agent => agent.Value.Enabled))
        {
            var provider = aiConfig.Providers[agentConfig.Provider];
            builder.Services.AddKeyedSingleton(agentName, (serviceProvider, _) =>
            {
                // Uses deferred resolution to avoid circular singleton dependencies.
                var tools = AgentExtensions.CreateToolsForAgent(serviceProvider, agentConfig, aiConfig,
                    deferResolution: true, isDevelopment: builder.Environment.IsDevelopment(),
                    instructionsAssembly: typeof(HausServiceCollectionExtensions).Assembly,
                    logger: serviceProvider.GetService<ILoggerFactory>()?.CreateLogger(nameof(AgentExtensions)));

                var (_, agent, _) = builder.CreateAgent(
                    provider,
                    agentConfig,
                    serviceProvider,
                    tools,
                    otelSourceName,
                    aiConfig: aiConfig);
                return agent;
            });
        }

        builder.Services.AddCommsStreamSink();
        builder.Services.AddFeatureFlagService(enabledFeatures);

        if (!enabledFeatures.Contains(FeatureNames.SignalRHub))
            return null;

        var signalRHubConfig = builder.Services.AddSignalRHub(builder.Configuration);
        builder.Services.AddSignalR()
            .AddStackExchangeRedis(connectionMultiplexer.Configuration, options =>
            {
                options.Configuration.ChannelPrefix = RedisChannel.Literal(FeatureNames.SignalRHub);
            });

        return signalRHubConfig;
    }
}