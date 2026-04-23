namespace CasCap.Models;

/// <summary>
/// A point-in-time snapshot of key Buderus heat pump sensor values retrieved from Azure Table Storage.
/// </summary>
public record BuderusSnapshot
{
    /// <summary>
    /// Domestic hot water circuit 1 actual temperature (°C).
    /// </summary>
    /// <remarks>Datapoint: <c>/dhwCircuits/dhw1/actualTemp</c></remarks>
    [Description("DHW circuit 1 actual temperature")]
    public double? Dhw1ActualTemp { get; init; }

    /// <summary>
    /// Domestic hot water circuit 1 set temperature (°C).
    /// </summary>
    /// <remarks>Datapoint: <c>/dhwCircuits/dhw1/setTemperature</c></remarks>
    [Description("DHW circuit 1 set temperature")]
    public double? Dhw1SetTemperature { get; init; }

    /// <summary>
    /// Domestic hot water circuit 1 current setpoint (°C).
    /// </summary>
    /// <remarks>Datapoint: <c>/dhwCircuits/dhw1/currentSetpoint</c></remarks>
    [Description("DHW circuit 1 current setpoint")]
    public double? Dhw1CurrentSetpoint { get; init; }

    /// <summary>
    /// Domestic hot water circuit 1 extra DHW stop temperature (°C).
    /// </summary>
    /// <remarks>Datapoint: <c>/dhwCircuits/dhw1/extraDhw/stopTemp</c></remarks>
    [Description("DHW circuit 1 extra DHW stop temperature")]
    public double? Dhw1ExtraDhwStopTemp { get; init; }

    /// <summary>
    /// Heating circuit 1 exception temperature level (°C).
    /// </summary>
    /// <remarks>Datapoint: <c>/heatingCircuits/hc1/temperatureLevels/exception</c></remarks>
    [Description("Heating circuit 1 exception temperature level")]
    public double? Hc1TemperatureException { get; init; }

    /// <summary>
    /// Heating circuit 1 normal temperature level (°C).
    /// </summary>
    /// <remarks>Datapoint: <c>/heatingCircuits/hc1/temperatureLevels/normal</c></remarks>
    [Description("Heating circuit 1 normal temperature level")]
    public double? Hc1TemperatureNormal { get; init; }

    /// <summary>
    /// Heating circuit 1 supply temperature setpoint (°C).
    /// </summary>
    /// <remarks>Datapoint: <c>/heatingCircuits/hc1/supplyTemperatureSetpoint</c></remarks>
    [Description("Heating circuit 1 supply temperature setpoint")]
    public double? Hc1SupplyTemperatureSetpoint { get; init; }

    /// <summary>
    /// Heating circuit 2 exception temperature level (°C).
    /// </summary>
    /// <remarks>Datapoint: <c>/heatingCircuits/hc2/temperatureLevels/exception</c></remarks>
    [Description("Heating circuit 2 exception temperature level")]
    public double? Hc2TemperatureException { get; init; }

    /// <summary>
    /// Heating circuit 2 normal temperature level (°C).
    /// </summary>
    /// <remarks>Datapoint: <c>/heatingCircuits/hc2/temperatureLevels/normal</c></remarks>
    [Description("Heating circuit 2 normal temperature level")]
    public double? Hc2TemperatureNormal { get; init; }

    /// <summary>
    /// Heating circuit 2 supply temperature setpoint (°C).
    /// </summary>
    /// <remarks>Datapoint: <c>/heatingCircuits/hc2/supplyTemperatureSetpoint</c></remarks>
    [Description("Heating circuit 2 supply temperature setpoint")]
    public double? Hc2SupplyTemperatureSetpoint { get; init; }

    /// <summary>
    /// Outdoor temperature sensor T1 (°C).
    /// </summary>
    /// <remarks>Datapoint: <c>/system/sensors/outdoorTemperatures/t1</c></remarks>
    [Description("Outdoor temperature")]
    public double? OutdoorTemperature { get; init; }

    /// <summary>
    /// Creates a <see cref="BuderusSnapshot"/> from a dictionary keyed by column name (i.e. property name).
    /// Used by the Redis sink where values are stored by <see cref="DatapointMapping.ColumnName"/>.
    /// </summary>
    internal static BuderusSnapshot FromValues(IReadOnlyDictionary<string, string> values)
    {
        if (values.Count == 0) return new();
        return new BuderusSnapshot
        {
            Dhw1ActualTemp = TryGetDouble(values, nameof(Dhw1ActualTemp)),
            Dhw1SetTemperature = TryGetDouble(values, nameof(Dhw1SetTemperature)),
            Dhw1CurrentSetpoint = TryGetDouble(values, nameof(Dhw1CurrentSetpoint)),
            Dhw1ExtraDhwStopTemp = TryGetDouble(values, nameof(Dhw1ExtraDhwStopTemp)),
            Hc1TemperatureException = TryGetDouble(values, nameof(Hc1TemperatureException)),
            Hc1TemperatureNormal = TryGetDouble(values, nameof(Hc1TemperatureNormal)),
            Hc1SupplyTemperatureSetpoint = TryGetDouble(values, nameof(Hc1SupplyTemperatureSetpoint)),
            Hc2TemperatureException = TryGetDouble(values, nameof(Hc2TemperatureException)),
            Hc2TemperatureNormal = TryGetDouble(values, nameof(Hc2TemperatureNormal)),
            Hc2SupplyTemperatureSetpoint = TryGetDouble(values, nameof(Hc2SupplyTemperatureSetpoint)),
            OutdoorTemperature = TryGetDouble(values, nameof(OutdoorTemperature)),
        };
    }

    #region private helpers

    private static double? TryGetDouble(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out var raw) && double.TryParse(raw, out var result) ? result : null;

    #endregion
}
