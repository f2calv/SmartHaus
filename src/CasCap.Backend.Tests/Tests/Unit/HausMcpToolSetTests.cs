using CasCap.Extensions;
using ModelContextProtocol.Server;
using System.Reflection;

namespace CasCap.Tests.Unit;

/// <summary>Verifies that each feature exposes only its own MCP tools and prompts.</summary>
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

    private static string[] GetToolTypes(string feature) =>
        [.. HausMcpServiceCollectionExtensions
            .GetToolSets(new HashSet<string>([feature], StringComparer.OrdinalIgnoreCase))
            .Select(set => set.ToolType.Name)
            .Order(StringComparer.Ordinal)];
}
