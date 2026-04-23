using Microsoft.Agents.AI;

namespace CasCap.Services;

/// <summary>Captures a single step in the agent pipeline timeline for the debug message.</summary>
public sealed record CommsDebugStep(
    string Label,
    string? Provider,
    TimeSpan WallClockOffset,
    AgentRunResult? Result = null);
