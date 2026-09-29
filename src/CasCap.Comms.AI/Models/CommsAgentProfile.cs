namespace CasCap.Models;

/// <summary>Identifies the agent profile that answers Signalizr communications.</summary>
/// <param name="AgentKey">The key of the agent in <see cref="AIConfig.Agents"/>, also the keyed <see cref="Microsoft.Agents.AI.AIAgent"/> service key.</param>
/// <param name="InstructionsAssembly">The assembly holding the embedded instruction resources the profile names.</param>
public sealed record CommsAgentProfile(string AgentKey, Assembly InstructionsAssembly);
