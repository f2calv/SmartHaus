using ModelContextProtocol.AspNetCore;

namespace CasCap.Extensions;

/// <summary>
/// Registers the MCP server with only the tool and prompt types whose features are enabled.
/// </summary>
public static class HausMcpServiceCollectionExtensions
{
    private static readonly HausMcpToolSet[] ToolSets =
    [
        new([FeatureNames.Buderus, FeatureNames.Comms], typeof(HeatPumpMcpQueryService), [typeof(HeatPumpMcpPrompts)],
            services => services.AddSingleton<HeatPumpMcpQueryService>()),
        new([FeatureNames.EdgeHardware, FeatureNames.Comms], typeof(EdgeHardwareMcpQueryService), [],
            services => services.AddSingleton<EdgeHardwareMcpQueryService>()),
        new([FeatureNames.DoorBird, FeatureNames.Comms], typeof(FrontDoorMcpQueryService), [typeof(FrontDoorMcpPrompts)],
            services => services.AddSingleton<FrontDoorMcpQueryService>()),
        new([FeatureNames.Knx, FeatureNames.Comms], typeof(BusSystemMcpQueryService), [typeof(BusSystemMcpPrompts)],
            services => services.AddSingleton<BusSystemMcpQueryService>()),
        new([FeatureNames.Fronius, FeatureNames.Comms], typeof(InverterMcpQueryService), [typeof(InverterMcpPrompts)],
            services => services.AddSingleton<InverterMcpQueryService>()),
        new([FeatureNames.Shelly, FeatureNames.Comms], typeof(SmartPlugMcpQueryService), [],
            services => services.AddSingleton<SmartPlugMcpQueryService>()),
        // Spans Wiz, KNX and Shelly, so each query service is optional.
        new([FeatureNames.Wiz, FeatureNames.Knx, FeatureNames.Shelly, FeatureNames.Comms], typeof(SmartLightingMcpQueryService), [],
            services => services.AddSingleton(sp => new SmartLightingMcpQueryService(
                sp.GetRequiredService<IOptions<KnxConfig>>(),
                sp.GetService<IKnxQueryService>(),
                sp.GetService<IWizQueryService>(),
                sp.GetService<IShellyQueryService>()))),
        new([FeatureNames.Ubiquiti], typeof(IpCameraMcpQueryService), [],
            services => services.AddSingleton<IpCameraMcpQueryService>()),
        new([FeatureNames.Miele], typeof(AppliancesMcpQueryService), [typeof(AppliancesMcpPrompts)],
            services => services.AddSingleton<AppliancesMcpQueryService>()),
        new([FeatureNames.Sicce], typeof(AquariumMcpQueryService), [],
            services => services.AddSingleton<AquariumMcpQueryService>()),
        // The Comms feature registers its poll service itself.
        new([FeatureNames.Comms], typeof(MessagingMcpQueryService), [], _ => { }),
    ];

    /// <summary>Gets every MCP tool set known to this application.</summary>
    public static IReadOnlyList<HausMcpToolSet> AllToolSets => ToolSets;

    /// <summary>Selects the tool sets exposed for the enabled features.</summary>
    /// <param name="enabledFeatures">The features enabled for this deployment.</param>
    public static IReadOnlyList<HausMcpToolSet> GetToolSets(IReadOnlySet<string> enabledFeatures) =>
        [.. ToolSets.Where(set => set.Features.Any(enabledFeatures.Contains))];

    /// <summary>
    /// Registers the stateless MCP server, exposing only the tools and prompts of enabled features.
    /// </summary>
    /// <remarks>
    /// Tools marked <see cref="RequiresApprovalAttribute"/> wait for approval in the chat group. Only a
    /// <c>Comms</c> host can ask for it; any other host refuses those tools rather than running them.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="enabledFeatures">The features enabled for this deployment.</param>
    public static void AddHausMcp(this IServiceCollection services, IReadOnlySet<string> enabledFeatures)
    {
        var toolSets = GetToolSets(enabledFeatures);
        foreach (var toolSet in toolSets)
            toolSet.Register(services);

        services.AddMcpServer()
            .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
            .WithTools(toolSets.Select(set => set.ToolType))
            .WithPrompts(toolSets.SelectMany(set => set.PromptTypes))
            .WithToolApproval();
    }
}
