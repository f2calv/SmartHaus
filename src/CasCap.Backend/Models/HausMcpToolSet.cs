namespace CasCap.Models;

/// <summary>One MCP tool type, its prompts, and the features that expose it.</summary>
/// <param name="Features">Features that expose the tool type; any one is sufficient.</param>
/// <param name="ToolType">The <see cref="McpServerToolTypeAttribute"/> type.</param>
/// <param name="PromptTypes">The <see cref="McpServerPromptTypeAttribute"/> types that accompany the tools.</param>
/// <param name="Register">Registers the tool type's backing service.</param>
public sealed record HausMcpToolSet(
    string[] Features,
    Type ToolType,
    Type[] PromptTypes,
    Action<IServiceCollection> Register);
