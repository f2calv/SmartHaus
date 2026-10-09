using System.Reflection;

namespace CasCap.Tests;

/// <summary>
/// SmartHaus MCP tool-catalog bindings used by domain evaluation scenario tests.
/// </summary>
public static class SmartHausEvaluationTestData
{
    /// <summary>The assembly holding the SmartHaus MCP query services and prompts.</summary>
    public static Assembly HausAssembly { get; } = typeof(BusSystemMcpQueryService).Assembly;

    /// <summary>Every SmartHaus MCP tool type, including the shared messaging poll tools from CasCap.Comms.AI.</summary>
    public static McpToolCatalog Catalog { get; } =
        McpToolCatalog.FromAssemblies(HausAssembly, typeof(MessagingMcpQueryService).Assembly);
}
