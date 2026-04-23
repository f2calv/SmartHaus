namespace CasCap.Services;

/// <summary>
/// MCP wrapper for <see cref="IUbiquitiQueryService"/> that exposes IP camera operations as MCP tools.
/// </summary>
[McpServerToolType]
public partial class IpCameraMcpQueryService(IUbiquitiQueryService ubiquitiQuerySvc)
{
    /// <inheritdoc cref="IUbiquitiQueryService.GetSnapshot"/>
    [McpServerTool]
    [Description("Current IP camera state — last event timestamps and counts for motion, smart detection (person, vehicle, animal, package), and ring events.")]
    public Task<UbiquitiSnapshot> GetCameraStatus() => ubiquitiQuerySvc.GetSnapshot();
}
