namespace CasCap.Models;

/// <summary>
/// Represents the measurable quantities reported by a Sicce water pump device.
/// Each value maps directly to the same-named property on <see cref="SicceEvent"/>
/// and carries its OTel metric name, unit, and description via <see cref="MetricAttribute"/>.
/// </summary>
public enum SicceFunction
{
    /// <summary>
    /// Device water temperature in degrees Celsius.
    /// </summary>
    [Metric("sicce.temperature", "Cel", Description = "Sicce water pump temperature in degrees Celsius")]
    Temperature,

    /// <summary>
    /// Pump power level as a ratio (0.05–1.0).
    /// </summary>
    [Metric("sicce.power", "1", Description = "Sicce pump power level — dimensionless ratio where 1.0 = 100%, 0.05 = 5%")]
    Power,
}

/// <summary>Sicce device model types.</summary>
public enum ModelType
{
    /// <summary>
    /// Syncra SDC
    /// </summary>
    SyncraSdc = 1,
    /// <summary>
    /// Syncra SDC PSK
    /// </summary>
    SyncraSdcPsk = 2,
    /// <summary>
    /// Syncra SDC 7.0
    /// </summary>
    SyncraSdc7 = 3,
    /// <summary>
    /// Syncra SDC 9.0
    /// </summary>
    SyncraSdc9 = 4,
    /// <summary>
    /// Syncra SDC 6.0
    /// </summary>
    SyncraSdc6 = 5,
    /// <summary>
    /// XSTREAM
    /// </summary>
    XStream = 6,
    /// <summary>
    /// PSK SDC
    /// </summary>
    PskSdc = 8,
    /// <summary>
    /// Syncra SDC 3.0
    /// </summary>
    SyncraSdc3 = 9,
    /// <summary>
    /// PSK SDC 1200
    /// </summary>
    PskSdc1200 = 10,
    /// <summary>
    /// PSK SDC 2600
    /// </summary>
    PskSdc2600 = 11,
    /// <summary>
    /// PSK SDC 4000
    /// </summary>
    PskSdc4000 = 12
}

/// <summary>Wave mode types for Sicce devices.</summary>
public enum ModeType
{
    /// <summary>
    /// No Mode
    /// </summary>
    NoMode = 0,
    /// <summary>
    /// Lagoonal Ripple
    /// </summary>
    LagoonalRipple = 1,
    /// <summary>
    /// Sharp Break
    /// </summary>
    SharpBreak = 2,
    /// <summary>
    /// Reef Crest
    /// </summary>
    ReefCrest = 3,
    /// <summary>
    /// Slow Current
    /// </summary>
    SlowCurrent = 4,
    /// <summary>
    /// FastCurrent
    /// </summary>
    FastCurrent = 5
}

/// <summary>
/// device_model_id === 6 xstream
/// </summary>
public enum ProgramModeType
{
    /// <summary>Constant speed mode.</summary>
    ConstantSpeed = 0,
    /// <summary>Low noise mode.</summary>
    LowNoise = 1,
    /// <summary>Medium noise mode.</summary>
    MediumNoise = 2,
    /// <summary>High noise mode.</summary>
    HighNoise = 3,
    /// <summary>Low sinusoidal noise mode.</summary>
    LowSinusoidalNoise = 4,
    /// <summary>High sinusoidal noise mode.</summary>
    HighSinusoidalNoise = 5,
}

/// <summary>
/// device_model_id === 6 xstream
/// </summary>
public enum ProgramType
{
    /// <summary>
    /// SINUSOIDAL
    /// </summary>
    SINUSOIDAL = 1,
    /// <summary>
    /// IMPULSE
    /// </summary>
    IMPULSE = 2,
    /// <summary>
    /// CONSTANT
    /// </summary>
    CONSTANT = 3,
    /// <summary>
    /// TIDAL
    /// </summary>
    TIDAL = 4
}
