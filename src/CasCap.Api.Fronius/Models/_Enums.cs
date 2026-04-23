namespace CasCap.Models;

/// <summary>
/// Represents the measurable quantities reported by a Fronius solar inverter.
/// Each value maps directly to the same-named property on <see cref="FroniusEvent"/>
/// and carries its OTel metric name, unit, and description via <see cref="MetricAttribute"/>.
/// </summary>
public enum FroniusFunction
{
    /// <summary>
    /// Battery State Of Charge expressed as a dimensionless ratio: <c>1.0</c> = 100 %,
    /// <c>0.5</c> = 50 %, <c>0.07</c> = 7 %. Use the UCUM unit <c>"1"</c> and multiply
    /// by 100 in dashboards to display as a percentage.
    /// </summary>
    [Metric("inverter.soc", "1", Description = "Battery State Of Charge — dimensionless ratio where 1.0 = 100%, 0.5 = 50%, 0.07 = 7%")]
    SOC,

    /// <summary>
    /// Net power flow at the battery terminals in Watts. A positive value means power is
    /// being drawn <em>from</em> the battery (discharging); a negative value means power is
    /// being pushed <em>into</em> the battery (charging).
    /// </summary>
    [Metric("inverter.battery.power", "W", Description = "Net battery power in Watts — positive = discharging, negative = charging")]
    P_Akku,

    /// <summary>
    /// Net power flow at the grid connection point in Watts. A positive value means power is
    /// being imported <em>from</em> the grid; a negative value means surplus power is being
    /// exported <em>to</em> the grid.
    /// </summary>
    [Metric("inverter.grid.power", "W", Description = "Net grid power in Watts — positive = importing, negative = exporting")]
    P_Grid,

    /// <summary>
    /// Total instantaneous electrical load of the house in Watts. This represents all
    /// consuming devices combined and is always a positive value.
    /// </summary>
    [Metric("inverter.load.power", "W", Description = "Total house load power consumption in Watts")]
    P_Load,

    /// <summary>
    /// Instantaneous solar photovoltaic generation power in Watts. This is the raw DC output
    /// of the PV array as seen by the inverter and is always a positive value (or zero at night).
    /// </summary>
    [Metric("inverter.pv.power", "W", Description = "Solar photovoltaic generation power in Watts")]
    P_PV,
}
