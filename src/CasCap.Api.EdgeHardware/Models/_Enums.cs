namespace CasCap.Models;

/// <summary>How much energy detail the debug chat reports for an agent turn.</summary>
public enum EnergyReportingMode
{
    /// <summary>Report no energy figure at all.</summary>
    Off,

    /// <summary>Report the watt-hours only, for example <c>0.31Wh</c>.</summary>
    Minimal,

    /// <summary>Report the watt-hours followed by the everyday equivalences.</summary>
    Verbose,
}
