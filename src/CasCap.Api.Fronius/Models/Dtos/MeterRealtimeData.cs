namespace CasCap.Models.Dtos;

/// <summary>
/// Meter real-time data from <c>GetMeterRealtimeData.cgi</c>.
/// Contains energy, power, current, voltage and frequency measurements.
/// </summary>
public record MeterRealtimeData
{
    /// <summary>
    /// Meter device details.
    /// </summary>
    [Description("Meter device details.")]
    public MeterDetails? Details { get; init; }

    /// <summary>
    /// AC current phase 1 in amps.
    /// </summary>
    [Description("AC current phase 1 in amps.")]
    public double Current_AC_Phase_1 { get; init; }

    /// <summary>
    /// AC current phase 2 in amps.
    /// </summary>
    [Description("AC current phase 2 in amps.")]
    public double Current_AC_Phase_2 { get; init; }

    /// <summary>
    /// AC current phase 3 in amps.
    /// </summary>
    [Description("AC current phase 3 in amps.")]
    public double Current_AC_Phase_3 { get; init; }

    /// <summary>
    /// AC current sum across all phases in amps.
    /// </summary>
    [Description("AC current sum across all phases in amps.")]
    public double Current_AC_Sum { get; init; }

    /// <summary>
    /// Whether the meter is enabled (1 = enabled).
    /// </summary>
    [Description("Whether the meter is enabled (1 = enabled).")]
    public int Enable { get; init; }

    /// <summary>
    /// Absolute energy consumed from the grid in watt-hours.
    /// </summary>
    [Description("Absolute energy consumed from the grid in watt-hours.")]
    public double EnergyReal_WAC_Minus_Absolute { get; init; }

    /// <summary>
    /// Absolute energy fed to the grid in watt-hours.
    /// </summary>
    [Description("Absolute energy fed to the grid in watt-hours.")]
    public double EnergyReal_WAC_Plus_Absolute { get; init; }

    /// <summary>
    /// Total real energy consumed in watt-hours.
    /// </summary>
    [Description("Total real energy consumed in watt-hours.")]
    public double EnergyReal_WAC_Sum_Consumed { get; init; }

    /// <summary>
    /// Total real energy produced in watt-hours.
    /// </summary>
    [Description("Total real energy produced in watt-hours.")]
    public double EnergyReal_WAC_Sum_Produced { get; init; }

    /// <summary>
    /// Average frequency across all phases in Hz.
    /// </summary>
    [Description("Average frequency across all phases in Hz.")]
    public double Frequency_Phase_Average { get; init; }

    /// <summary>
    /// Current meter location (0 = grid, 1 = load).
    /// </summary>
    [Description("Current meter location (0 = grid, 1 = load).")]
    public int Meter_Location_Current { get; init; }

    /// <summary>
    /// Apparent power phase 1 in VA.
    /// </summary>
    [Description("Apparent power phase 1 in VA.")]
    public double PowerApparent_S_Phase_1 { get; init; }

    /// <summary>
    /// Apparent power phase 2 in VA.
    /// </summary>
    [Description("Apparent power phase 2 in VA.")]
    public double PowerApparent_S_Phase_2 { get; init; }

    /// <summary>
    /// Apparent power phase 3 in VA.
    /// </summary>
    [Description("Apparent power phase 3 in VA.")]
    public double PowerApparent_S_Phase_3 { get; init; }

    /// <summary>
    /// Total apparent power in VA.
    /// </summary>
    [Description("Total apparent power in VA.")]
    public double PowerApparent_S_Sum { get; init; }

    /// <summary>
    /// Power factor phase 1.
    /// </summary>
    [Description("Power factor phase 1.")]
    public double PowerFactor_Phase_1 { get; init; }

    /// <summary>
    /// Power factor phase 2.
    /// </summary>
    [Description("Power factor phase 2.")]
    public double PowerFactor_Phase_2 { get; init; }

    /// <summary>
    /// Power factor phase 3.
    /// </summary>
    [Description("Power factor phase 3.")]
    public double PowerFactor_Phase_3 { get; init; }

    /// <summary>
    /// Total power factor.
    /// </summary>
    [Description("Total power factor.")]
    public double PowerFactor_Sum { get; init; }

    /// <summary>
    /// Reactive power phase 1 in VAr.
    /// </summary>
    [Description("Reactive power phase 1 in VAr.")]
    public double PowerReactive_Q_Phase_1 { get; init; }

    /// <summary>
    /// Reactive power phase 2 in VAr.
    /// </summary>
    [Description("Reactive power phase 2 in VAr.")]
    public double PowerReactive_Q_Phase_2 { get; init; }

    /// <summary>
    /// Reactive power phase 3 in VAr.
    /// </summary>
    [Description("Reactive power phase 3 in VAr.")]
    public double PowerReactive_Q_Phase_3 { get; init; }

    /// <summary>
    /// Total reactive power in VAr.
    /// </summary>
    [Description("Total reactive power in VAr.")]
    public double PowerReactive_Q_Sum { get; init; }

    /// <summary>
    /// Real power phase 1 in watts.
    /// </summary>
    [Description("Real power phase 1 in watts.")]
    public double PowerReal_P_Phase_1 { get; init; }

    /// <summary>
    /// Real power phase 2 in watts.
    /// </summary>
    [Description("Real power phase 2 in watts.")]
    public double PowerReal_P_Phase_2 { get; init; }

    /// <summary>
    /// Real power phase 3 in watts.
    /// </summary>
    [Description("Real power phase 3 in watts.")]
    public double PowerReal_P_Phase_3 { get; init; }

    /// <summary>
    /// Total real power in watts.
    /// </summary>
    [Description("Total real power in watts.")]
    public double PowerReal_P_Sum { get; init; }

    /// <summary>
    /// Unix timestamp of the measurement.
    /// </summary>
    [Description("Unix timestamp of the measurement.")]
    public long Timestamp { get; init; }

    /// <summary>
    /// Whether the meter is visible in the UI (1 = visible).
    /// </summary>
    [Description("Whether the meter is visible in the UI (1 = visible).")]
    public int Visible { get; init; }

    /// <summary>
    /// Phase-to-phase voltage L1-L2 in volts.
    /// </summary>
    [Description("Phase-to-phase voltage L1-L2 in volts.")]
    public double Voltage_AC_PhaseToPhase_12 { get; init; }

    /// <summary>
    /// Phase-to-phase voltage L2-L3 in volts.
    /// </summary>
    [Description("Phase-to-phase voltage L2-L3 in volts.")]
    public double Voltage_AC_PhaseToPhase_23 { get; init; }

    /// <summary>
    /// Phase-to-phase voltage L3-L1 in volts.
    /// </summary>
    [Description("Phase-to-phase voltage L3-L1 in volts.")]
    public double Voltage_AC_PhaseToPhase_31 { get; init; }

    /// <summary>
    /// Phase 1 voltage in volts.
    /// </summary>
    [Description("Phase 1 voltage in volts.")]
    public double Voltage_AC_Phase_1 { get; init; }

    /// <summary>
    /// Phase 2 voltage in volts.
    /// </summary>
    [Description("Phase 2 voltage in volts.")]
    public double Voltage_AC_Phase_2 { get; init; }

    /// <summary>
    /// Phase 3 voltage in volts.
    /// </summary>
    [Description("Phase 3 voltage in volts.")]
    public double Voltage_AC_Phase_3 { get; init; }
}
