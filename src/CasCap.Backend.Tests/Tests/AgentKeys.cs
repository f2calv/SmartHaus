namespace CasCap.Constants;

/// <summary>Well-known remote Agent Runtime definition names.</summary>
public static class AgentKeys
{
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
}
