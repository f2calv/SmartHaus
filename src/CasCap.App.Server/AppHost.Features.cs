using CasCap.Constants;
using StackExchange.Redis;

namespace CasCap;

public static partial class AppHost
{
    private static SignalRHubConfig? AddFeatures(
        WebApplicationBuilder builder,
        AppConfig appConfig,
        IReadOnlySet<string> enabledFeatures,
        IConnectionMultiplexer connectionMultiplexer)
    {
        // Register SignalR services unconditionally so IHubContext<> is always resolvable for
        // HausHub sinks discovered during assembly scanning (e.g. BuderusSinkSignalRService).
        // The hub endpoint mapping and Redis backplane are configured later when SignalRHub is enabled.
        builder.Services.AddSignalR();

        if (enabledFeatures.Contains(FeatureNames.Buderus) || enabledFeatures.Contains(FeatureNames.Comms))
        {
            builder.Services.AddBuderusWithExtraSinks(builder.Configuration,
                lite: !enabledFeatures.Contains(FeatureNames.Buderus),
                additionalSinkAssemblies: [typeof(HausServiceCollectionExtensions).Assembly]);
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
        }

        if (enabledFeatures.Contains(FeatureNames.DDns))
            builder.Services.AddDDns(builder.Configuration);

        if (enabledFeatures.Contains(FeatureNames.Knx) || enabledFeatures.Contains(FeatureNames.Comms))
        {
            builder.Services.AddKnxWithExtraSinks(builder.Configuration,
                lite: !enabledFeatures.Contains(FeatureNames.Knx),
                additionalSinkAssemblies: [typeof(HausServiceCollectionExtensions).Assembly]);
        }

        if (enabledFeatures.Contains(FeatureNames.Fronius) || enabledFeatures.Contains(FeatureNames.Comms))
        {
            builder.Services.AddFroniusWithExtraSinks(builder.Configuration,
                lite: !enabledFeatures.Contains(FeatureNames.Fronius),
                additionalSinkAssemblies: [typeof(HausServiceCollectionExtensions).Assembly]);
        }

        if (enabledFeatures.Contains(FeatureNames.Shelly) || enabledFeatures.Contains(FeatureNames.Comms))
        {
            builder.Services.AddShellyWithExtraSinks(builder.Configuration,
                lite: !enabledFeatures.Contains(FeatureNames.Shelly),
                additionalSinkAssemblies: [typeof(HausServiceCollectionExtensions).Assembly]);
        }

        if (enabledFeatures.Contains(FeatureNames.Ubiquiti))
        {
            builder.Services.AddMediaStreamSink();
            builder.Services.AddCameraClipCapture();
            builder.Services.AddUbiquitiWithExtraSinks(builder.Configuration,
                lite: !enabledFeatures.Contains(FeatureNames.Ubiquiti),
                additionalSinkAssemblies: [typeof(HausServiceCollectionExtensions).Assembly]);
        }

        if (enabledFeatures.Contains(FeatureNames.Miele))
        {
            builder.Services.AddMieleWithExtraSinks(builder.Configuration,
                lite: !enabledFeatures.Contains(FeatureNames.Miele),
                additionalSinkAssemblies: [typeof(HausServiceCollectionExtensions).Assembly]);
        }

        if (enabledFeatures.Contains(FeatureNames.Comms))
            builder.AddComms();

        builder.Services.AddHausMcp(enabledFeatures);
        builder.Services.AddTrackedOperations();
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