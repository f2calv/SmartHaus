using Azure.Core;
using System.Reflection;

namespace CasCap.Tests;

/// <summary>
/// SmartHaus bindings for the <c>CasCap.Common.AI.Evaluation</c> harness: the assembly holding the MCP tool
/// types and embedded agent instructions, and the model evaluated when none is configured.
/// </summary>
public static class SmartHausEvaluationTestData
{
    /// <summary>Provider evaluated when <see cref="AgentEvaluationConfig.ProviderKeys"/> is empty: the production edge model.</summary>
    public const string DefaultProviderKey = "EdgeGpu";

    /// <summary>The assembly holding the MCP query services, MCP prompts and embedded agent instructions.</summary>
    public static Assembly HausAssembly { get; } = typeof(SystemMcpQueryService).Assembly;

    /// <summary>Every SmartHaus MCP tool type, including the shared messaging poll tools from CasCap.Comms.AI.</summary>
    public static McpToolCatalog Catalog { get; } =
        McpToolCatalog.FromAssemblies(HausAssembly, typeof(MessagingMcpQueryService).Assembly);

    /// <summary>The configured provider keys, or <see cref="DefaultProviderKey"/> when none are configured.</summary>
    /// <param name="config">The evaluation configuration.</param>
    public static IReadOnlyList<string> GetProviderKeys(AgentEvaluationConfig config) =>
        config.ProviderKeys.Length > 0 ? config.ProviderKeys : [DefaultProviderKey];

    /// <summary>Creates a harness over the SmartHaus agents.</summary>
    /// <param name="loggerFactory">Routes agent diagnostics to the test output.</param>
    /// <param name="aiConfig">The SmartHaus agent and provider configuration.</param>
    /// <param name="tokenCredential">Entra ID credential for Azure OpenAI providers, or <see langword="null"/>.</param>
    /// <param name="config">Evaluation switches, or <see langword="null"/> for the defaults.</param>
    /// <param name="throttlingMonitor">Counts Azure SDK throttling, or <see langword="null"/>.</param>
    public static AgentEvaluationHarness CreateHarness(ILoggerFactory loggerFactory, AIConfig aiConfig,
        TokenCredential? tokenCredential = null, AgentEvaluationConfig? config = null,
        AzureThrottlingMonitor? throttlingMonitor = null) =>
        new(loggerFactory, aiConfig, Catalog, HausAssembly, tokenCredential, config, throttlingMonitor);
}
