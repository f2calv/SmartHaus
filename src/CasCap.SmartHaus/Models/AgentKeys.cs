namespace CasCap.Models;

/// <summary>Well-known constants for <c>AIConfig.Agents</c> configuration paths.</summary>
/// <remarks>
/// Use these constants instead of string literals to keep configuration paths
/// refactor-safe. The path segment constants mirror the corresponding names on
/// <c>AIConfig</c> and <c>AgentConfig</c> (defined in CasCap.Common.AI) without
/// introducing a project reference.
/// </remarks>
public static class AgentKeys
{
    /// <summary>Root configuration section name (<c>"AIConfig"</c>).</summary>
    public const string AIConfig = nameof(AIConfig);

    /// <summary>Configuration path segment for the <c>Agents</c> dictionary.</summary>
    public const string Agents = nameof(Agents);

    /// <summary>Configuration path segment for the agent-specific <c>Settings</c> sub-section.</summary>
    public const string Settings = nameof(Settings);

    /// <summary>Security / DoorBird vision agent.</summary>
    public const string SecurityAgent = nameof(SecurityAgent);

    /// <summary>Heating / Buderus DHW agent.</summary>
    public const string HeatingAgent = nameof(HeatingAgent);

    /// <summary>Communications / Signal messenger agent.</summary>
    public const string CommsAgent = nameof(CommsAgent);

    /// <summary>Solar inverter / energy agent.</summary>
    public const string EnergyAgent = nameof(EnergyAgent);

    /// <summary>Home control agent — KNX lighting, shutters, outlets, rooms, diagnostics.</summary>
    public const string HomeControlAgent = nameof(HomeControlAgent);

    /// <summary>Edge infrastructure / GPU monitoring agent.</summary>
    public const string InfraAgent = nameof(InfraAgent);

    /// <summary>Home Connect appliances agent.</summary>
    public const string AppliancesAgent = nameof(AppliancesAgent);

    /// <summary>Audio transcription agent (Whisper model).</summary>
    public const string AudioAgent = nameof(AudioAgent);
}
