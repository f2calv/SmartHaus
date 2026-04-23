namespace CasCap.Models;

/// <summary>
/// Azure Table Storage extension methods for <see cref="BuderusSnapshot"/>.
/// </summary>
internal static class BuderusSnapshotExtensions
{
    /// <summary>
    /// Creates a <see cref="BuderusSnapshot"/> from a snapshot <see cref="TableEntity"/>.
    /// Column names are matched to properties via <c>nameof()</c> for compile-time safety.
    /// </summary>
    /// <remarks>
    /// The <see cref="DatapointMapping.ColumnName"/> values in <see cref="BuderusConfig.DatapointMappings"/>
    /// must use these same property names as column names so the write path and read path stay in sync.
    /// </remarks>
    internal static BuderusSnapshot FromTableEntity(TableEntity? entity)
    {
        if (entity is null) return new();
        return new BuderusSnapshot
        {
            Dhw1ActualTemp = TryGetDouble(entity, nameof(BuderusSnapshot.Dhw1ActualTemp)),
            Dhw1SetTemperature = TryGetDouble(entity, nameof(BuderusSnapshot.Dhw1SetTemperature)),
            Dhw1CurrentSetpoint = TryGetDouble(entity, nameof(BuderusSnapshot.Dhw1CurrentSetpoint)),
            Dhw1ExtraDhwStopTemp = TryGetDouble(entity, nameof(BuderusSnapshot.Dhw1ExtraDhwStopTemp)),
            Hc1TemperatureException = TryGetDouble(entity, nameof(BuderusSnapshot.Hc1TemperatureException)),
            Hc1TemperatureNormal = TryGetDouble(entity, nameof(BuderusSnapshot.Hc1TemperatureNormal)),
            Hc1SupplyTemperatureSetpoint = TryGetDouble(entity, nameof(BuderusSnapshot.Hc1SupplyTemperatureSetpoint)),
            Hc2TemperatureException = TryGetDouble(entity, nameof(BuderusSnapshot.Hc2TemperatureException)),
            Hc2TemperatureNormal = TryGetDouble(entity, nameof(BuderusSnapshot.Hc2TemperatureNormal)),
            Hc2SupplyTemperatureSetpoint = TryGetDouble(entity, nameof(BuderusSnapshot.Hc2SupplyTemperatureSetpoint)),
            OutdoorTemperature = TryGetDouble(entity, nameof(BuderusSnapshot.OutdoorTemperature)),
        };
    }

    private static double? TryGetDouble(TableEntity entity, string column)
        => entity.TryGetValue(column, out var raw) && double.TryParse(raw?.ToString(), out var result) ? result : null;
}
