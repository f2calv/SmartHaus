using System.Text.Json.Serialization;

namespace CasCap.Models;

/// <summary>Buderus KM200 gauge types corresponding to datapoint paths.</summary>
public enum BuderusGauges
{
    /// <summary>Not known gauge type</summary>
    Unknown,
    /// <summary>DHV circuit 1 actual temperature</summary>
    dhwCircuits_dhw1_actualTemp,
    /// <summary>DHV circuit 1 set temperature</summary>
    dhwCircuits_dhw1_setTemperature,
    /// <summary>DHV circuit 1 current setpoint</summary>
    dhwCircuits_dhw1_currentSetpoint,
    /// <summary>DHV circuit 1 extra DHW stop temperature</summary>
    dhwCircuits_dhw1_extraDhw_stopTemp,
    /// <summary>Heating circuit 1 temperature levels exception</summary>
    heatingCircuits_hc1_temperatureLevels_exception,
    /// <summary>Heating circuit 1 temperature levels normal</summary>
    heatingCircuits_hc1_temperatureLevels_normal,
    /// <summary>Heating circuit 1 supply temperature setpoint</summary>
    heatingCircuits_hc1_supplyTemperatureSetpoint,
    /// <summary>Heating circuit 2 temperature levels exception</summary>
    heatingCircuits_hc2_temperatureLevels_exception,
    /// <summary>Heating circuit 2 temperature levels normal</summary>
    heatingCircuits_hc2_temperatureLevels_normal,
    /// <summary>Heating circuit 2 supply temperature setpoint</summary>
    heatingCircuits_hc2_supplyTemperatureSetpoint,
    /// <summary>Outdoor temperature sensor T1</summary>
    system_sensors_outdoorTemperatures_t1,
    /// <summary>Setpoint scheduler normal temperature for heating circuit 1</summary>
    application_setpointScheduler_hc1_normalTemp,
    /// <summary>Setpoint scheduler normal temperature for heating circuit 2</summary>
    application_setpointScheduler_hc2_normalTemp,
    /// <summary>Setpoint scheduler normal temperature for heating circuit 3</summary>
    application_setpointScheduler_hc3_normalTemp,
    /// <summary>Setpoint scheduler normal temperature for heating circuit 4</summary>
    application_setpointScheduler_hc4_normalTemp
}

/// <summary>KM200 datapoint types.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MyDatapointType
{
    /// <summary>Reference enumeration type.</summary>
    refEnum,

    /// <summary>Floating point value type.</summary>
    floatValue,

    /// <summary>String value type.</summary>
    stringValue,

    /// <summary>Switch program type.</summary>
    switchProgram,

    /// <summary>Y-axis recording type.</summary>
    yRecording,

    /// <summary>Array data type.</summary>
    arrayData
}
