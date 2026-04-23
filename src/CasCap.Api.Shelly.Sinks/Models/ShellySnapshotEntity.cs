namespace CasCap.Models;

/// <summary>
/// Azure Table Storage snapshot entity storing the latest Shelly smart plug values.
/// RowKey is the DeviceId so each device gets its own snapshot row under the "summary" partition.
/// </summary>
public class ShellySnapshotEntity : ITableEntity
{
    /// <summary>Initializes a new instance of the <see cref="ShellySnapshotEntity"/> class.</summary>
    public ShellySnapshotEntity() { }

    /// <summary>Initializes a new instance from a <see cref="ShellyEvent"/>.</summary>
    public ShellySnapshotEntity(string partitionKey, ShellyEvent evt)
    {
        PartitionKey = partitionKey;
        RowKey = evt.DeviceId;

        DeviceName = evt.DeviceName;
        Power = Math.Round(evt.Power, 1);
        RelayState = Math.Round(evt.RelayState, 0);
        Temperature = Math.Round(evt.Temperature, 1);
        Overpower = Math.Round(evt.Overpower, 0);
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

    /// <summary>Human-readable device name.</summary>
    [Description("Human-readable device name")]
    public string DeviceName { get; init; } = default!;

    /// <summary>Instantaneous power consumption (W).</summary>
    [Description("Instantaneous power consumption in watts")]
    public double Power { get; init; }

    /// <summary>Relay state (1 = on, 0 = off).</summary>
    [Description("Relay state — 1 = on, 0 = off")]
    public double RelayState { get; init; }

    /// <summary>Device temperature (°C).</summary>
    [Description("Device temperature in Celsius")]
    public double Temperature { get; init; }

    /// <summary>Overpower condition (1 = triggered, 0 = normal).</summary>
    [Description("Overpower condition — 1 = triggered, 0 = normal")]
    public double Overpower { get; init; }

    /// <summary>UTC timestamp of the last reading.</summary>
    public DateTimeOffset? ReadingUtc { get; init; }

    /// <summary>
    /// Generate a <see cref="TableEntity" /> from this <see cref="ShellySnapshotEntity" />.
    /// </summary>
    public TableEntity GetEntity()
    {
        var entity = new TableEntity(PartitionKey, RowKey)
        {
            { nameof(DeviceName), DeviceName },
            { nameof(Power), Power },
            { nameof(RelayState), RelayState },
            { nameof(Temperature), Temperature },
            { nameof(Overpower), Overpower },
            { nameof(ReadingUtc), ReadingUtc },
        };
        return entity;
    }
}
