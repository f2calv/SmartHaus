using CasCap.Extensions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Reflection;

namespace CasCap.Tests.Unit;

/// <summary>Verifies that each feature exposes only its own MCP tools and prompts, with explicit risk hints.</summary>
[Trait("Category", "Mcp")]
public sealed class HausMcpToolSetTests
{
    private static readonly Assembly BackendAssembly = typeof(HausMcpServiceCollectionExtensions).Assembly;

    [Fact]
    public void AllToolSets_CatalogueEveryToolTypeOnce()
    {
        var toolTypes = BackendAssembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .Append(typeof(MessagingMcpQueryService))
            .OrderBy(type => type.Name, StringComparer.Ordinal);

        var catalogued = HausMcpServiceCollectionExtensions.AllToolSets
            .Select(set => set.ToolType)
            .OrderBy(type => type.Name, StringComparer.Ordinal);

        Assert.Equal(toolTypes, catalogued);
    }

    [Fact]
    public void AllToolSets_CatalogueEveryPromptTypeOnce()
    {
        var promptTypes = BackendAssembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerPromptTypeAttribute>() is not null)
            .OrderBy(type => type.Name, StringComparer.Ordinal);

        var catalogued = HausMcpServiceCollectionExtensions.AllToolSets
            .SelectMany(set => set.PromptTypes)
            .OrderBy(type => type.Name, StringComparer.Ordinal);

        Assert.Equal(promptTypes, catalogued);
    }

    [Fact]
    public void GetToolSets_CommsExposesOnlyItsRemoteAgentTools()
    {
        var toolTypes = GetToolTypes(FeatureNames.Comms);

        Assert.Equal(
            [
                nameof(BusSystemMcpQueryService),
                nameof(EdgeHardwareMcpQueryService),
                nameof(FrontDoorMcpQueryService),
                nameof(HeatPumpMcpQueryService),
                nameof(InverterMcpQueryService),
                nameof(MessagingMcpQueryService),
                nameof(SmartLightingMcpQueryService),
                nameof(SmartPlugMcpQueryService),
            ],
            toolTypes);
    }

    [Theory]
    [InlineData(FeatureNames.Buderus, nameof(HeatPumpMcpQueryService))]
    [InlineData(FeatureNames.DoorBird, nameof(FrontDoorMcpQueryService))]
    [InlineData(FeatureNames.EdgeHardware, nameof(EdgeHardwareMcpQueryService))]
    [InlineData(FeatureNames.Fronius, nameof(InverterMcpQueryService))]
    [InlineData(FeatureNames.Miele, nameof(AppliancesMcpQueryService))]
    [InlineData(FeatureNames.Sicce, nameof(AquariumMcpQueryService))]
    [InlineData(FeatureNames.Ubiquiti, nameof(IpCameraMcpQueryService))]
    [InlineData(FeatureNames.Wiz, nameof(SmartLightingMcpQueryService))]
    public void GetToolSets_HardwareFeatureExposesOnlyItsOwnTools(string feature, string toolType) =>
        Assert.Equal([toolType], GetToolTypes(feature));

    [Theory]
    [InlineData(FeatureNames.Test)]
    [InlineData(FeatureNames.SignalRHub)]
    [InlineData(FeatureNames.DDns)]
    public void GetToolSets_FeatureWithoutToolsExposesNothing(string feature) =>
        Assert.Empty(GetToolTypes(feature));

    [Fact]
    public void AllTools_DeclareExplicitRiskHints()
    {
        var unannotated = GetAnnotations()
            .Where(tool => tool.Annotations?.ReadOnlyHint is null || tool.Annotations.DestructiveHint is null)
            .Select(tool => tool.Name);

        Assert.Empty(unannotated);
    }

    [Theory]
    [InlineData("unlock_house_door")]
    [InlineData("set_heat_pump_data_point")]
    [InlineData("execute_appliance_action")]
    [InlineData("start_appliance_program")]
    public void SafetySensitiveTools_AreDestructive(string toolName)
    {
        var annotations = GetAnnotations().Single(tool => tool.Name == toolName).Annotations!;

        Assert.False(annotations.ReadOnlyHint);
        Assert.True(annotations.DestructiveHint);
    }

    private static IEnumerable<(string Name, ToolAnnotations? Annotations)> GetAnnotations() =>
        HausMcpServiceCollectionExtensions.AllToolSets
            .SelectMany(set => set.ToolType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .Select(method => McpServerTool.Create(method, _ => null!).ProtocolTool)
            .Select(tool => (tool.Name, tool.Annotations));

    private static string[] GetToolTypes(string feature) =>
        [.. HausMcpServiceCollectionExtensions
            .GetToolSets(new HashSet<string>([feature], StringComparer.OrdinalIgnoreCase))
            .Select(set => set.ToolType.Name)
            .Order(StringComparer.Ordinal)];
}
