using CasCap.Constants;

namespace CasCap.App.Console;

public static partial class AppHost
{
    private static void AddFeatures(
        HostApplicationBuilder builder,
        IReadOnlySet<string> enabledFeatures)
    {
        builder.Services.AddCasCapCaching(builder.Configuration);

        // SystemMcpQueryService is referenced by all agents - register unconditionally.
        builder.Services.AddSystemMcp();

        if (enabledFeatures.Contains(FeatureNames.Buderus))
        {
            builder.Services.AddBuderusWithExtraSinks(builder.Configuration, lite: true);
            builder.Services.AddHeatPumpMcp();
        }

        if (enabledFeatures.Contains(FeatureNames.Wiz))
            builder.Services.AddWizWithExtraSinks(builder.Configuration, lite: true);

        if (enabledFeatures.Contains(FeatureNames.EdgeHardware))
        {
            builder.Services.AddEdgeHardwareWithExtraSinks(builder.Configuration, lite: true);
            builder.Services.AddEdgeHardwareMcp();
        }

        if (enabledFeatures.Contains(FeatureNames.DoorBird))
        {
            builder.Services.AddDoorBirdWithExtraSinks(builder.Configuration, lite: true);
            builder.Services.AddFrontDoorMcp();
        }

        if (enabledFeatures.Contains(FeatureNames.Knx))
        {
            builder.Services.AddKnxWithExtraSinks(builder.Configuration, lite: true);
            builder.Services.AddBusSystemMcp();
        }

        if (enabledFeatures.Contains(FeatureNames.Fronius))
        {
            builder.Services.AddFroniusWithExtraSinks(builder.Configuration, lite: true);
            builder.Services.AddInverterMcp();
        }

        if (enabledFeatures.Contains(FeatureNames.Shelly))
        {
            builder.Services.AddShellyWithExtraSinks(builder.Configuration, lite: true);
            builder.Services.AddSmartPlugMcp();
        }

        // SmartLightingMcp spans Wiz, KNX and Shelly - all three DI parameters are nullable.
        if (enabledFeatures.Contains(FeatureNames.Wiz) || enabledFeatures.Contains(FeatureNames.Knx)
            || enabledFeatures.Contains(FeatureNames.Shelly))
            builder.Services.AddSmartLightingMcp();

        if (enabledFeatures.Contains(FeatureNames.Ubiquiti))
        {
            builder.Services.AddUbiquitiWithExtraSinks(builder.Configuration, lite: true);
            builder.Services.AddCamerasMcp();
        }

        if (enabledFeatures.Contains(FeatureNames.Miele))
        {
            builder.Services.AddMieleWithExtraSinks(builder.Configuration, lite: true);
            builder.Services.AddAppliancesMcp();
        }

        // Agent configurations can reference messaging without requiring Signal CLI or Comms infrastructure.
        builder.Services.AddMessagingMcpStub();
        builder.Services.AddFeatureFlagService(enabledFeatures);

        // Index tool service and prompt types after all MCP registrations.
        builder.Services.AddAgentTypeRegistry(typeof(HausServiceCollectionExtensions).Assembly);
        builder.Services.AddSingleton<ConsoleApp>();
    }
}
