namespace CasCap.Services;

/// <summary>MCP tools for querying aquarium water pump status and readings.</summary>
[McpServerToolType]
public class AquariumMcpQueryService(ISicceQueryService sicceSvc)
{
    /// <summary>Retrieves the current aquarium pump snapshot.</summary>
    [McpServerTool]
    [Description("Returns the latest aquarium water pump readings including temperature, power level, and online status.")]
    public Task<SicceSnapshot> GetAquariumPumpStatus(CancellationToken cancellationToken = default) =>
        sicceSvc.GetSnapshot();

    /// <summary>Retrieves device information from the pump cloud API.</summary>
    [McpServerTool]
    [Description("Returns device information for the aquarium water pump directly from the cloud API.")]
    public Task<DeviceInfo?> GetAquariumPumpDeviceInfo(CancellationToken cancellationToken = default) =>
        sicceSvc.GetDeviceInfo();
}
