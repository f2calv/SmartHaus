namespace CasCap.Services;

/// <summary>
/// MCP wrapper for <see cref="IEdgeHardwareQueryService"/> that exposes edge hardware monitoring data as MCP tools.
/// </summary>
[McpServerToolType]
public partial class EdgeHardwareMcpQueryService(IEdgeHardwareQueryService edgeHardwareQuerySvc)
{
    /// <summary>
    /// Returns the latest edge hardware snapshots (GPU and CPU metrics) for all nodes.
    /// </summary>
    [McpServerTool]
    [Description("Current edge hardware metrics for all nodes. Each snapshot has a HasGpu flag — when false the node has no GPU and all Gpu* fields are null. Only attribute GPU data to nodes where HasGpu is true.")]
    public Task<List<EdgeHardwareSnapshot>> GetEdgeHardwareSnapshots() =>
        edgeHardwareQuerySvc.GetLatestSnapshots();
}
