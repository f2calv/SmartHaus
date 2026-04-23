namespace CasCap.Models;

/// <summary>
/// Azure Table Storage snapshot entity storing the latest Sicce device values.
/// </summary>
public class SicceSnapshotEntity : ITableEntity
{
    /// <summary>Parameterless constructor required by Azure Table Storage SDK.</summary>
    public SicceSnapshotEntity() { }

    /// <summary>Initializes a new instance from a <see cref="SicceEvent"/>.</summary>
    public SicceSnapshotEntity(string partitionKey, string rowKey, SicceEvent evt)
    {
        PartitionKey = partitionKey;
        RowKey = rowKey;

        Temperature = Math.Round(evt.Temperature, 1);
        Power = evt.Power;
        IsOnline = evt.IsOnline;
        PowerSwitch = evt.PowerSwitch;
        ReadingUtc = new DateTimeOffset(evt.TimestampUtc, TimeSpan.Zero);
    }

    /// <inheritdoc />
    public string PartitionKey { get; set; } = default!;

    /// <inheritdoc />
    public string RowKey { get; set; } = default!;

    /// <inheritdoc />
    public DateTimeOffset? Timestamp { get; set; }

    /// <inheritdoc />
    public ETag ETag { get; set; }

    /// <summary>Device temperature in degrees Celsius.</summary>
    [Description("Device temperature in degrees Celsius")]
    public double Temperature { get; init; }

    /// <summary>Power level as a ratio (0.05–1.0).</summary>
    [Description("Pump power level")]
    public double Power { get; init; }

    /// <summary>Whether the device is online.</summary>
    [Description("Device online status")]
    public bool IsOnline { get; init; }

    /// <summary>Whether the power switch is on.</summary>
    [Description("Power switch status")]
    public bool PowerSwitch { get; init; }

    /// <summary>UTC timestamp of the last reading.</summary>
    public DateTimeOffset? ReadingUtc { get; init; }

    /// <summary>
    /// Generate a <see cref="TableEntity"/> from this <see cref="SicceSnapshotEntity"/>.
    /// </summary>
    public TableEntity GetEntity()
    {
        var entity = new TableEntity(PartitionKey, RowKey)
        {
            { nameof(Temperature), Temperature },
            { nameof(Power), Power },
            { nameof(IsOnline), IsOnline },
            { nameof(PowerSwitch), PowerSwitch },
            { nameof(ReadingUtc), ReadingUtc },
        };
        return entity;
    }
}
