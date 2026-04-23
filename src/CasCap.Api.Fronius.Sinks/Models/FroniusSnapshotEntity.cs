namespace CasCap.Models;

/// <summary>
/// Azure Table Storage snapshot entity storing the latest Fronius inverter values.
/// </summary>
public class FroniusSnapshotEntity : ITableEntity
{
    /// <summary>Initializes a new instance of the <see cref="FroniusSnapshotEntity"/> class.</summary>
    public FroniusSnapshotEntity() { }

    /// <summary>Initializes a new instance from a <see cref="FroniusEvent"/>.</summary>
    public FroniusSnapshotEntity(string partitionKey, string rowKey, FroniusEvent evt)
    {
        PartitionKey = partitionKey;
        RowKey = rowKey;

        SOC = Math.Round(evt.SOC, 1);
        P_Akku = Math.Round(evt.P_Akku, 1);
        P_Grid = Math.Round(evt.P_Grid, 1);
        P_Load = Math.Round(evt.P_Load, 1);
        P_PV = Math.Round(evt.P_PV, 1);
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

    /// <summary>Battery state of charge (%).</summary>
    [Description("Battery charge percentage")]
    public double SOC { get; init; }

    /// <summary>Battery power (W). Positive = charging, negative = discharging.</summary>
    [Description("Battery charge remaining")]
    public double P_Akku { get; init; }

    /// <summary>Grid power (W). Positive = consuming, negative = feeding.</summary>
    [Description("Grid electrical power consumption")]
    public double P_Grid { get; init; }

    /// <summary>Household load power (W).</summary>
    [Description("Electrical power consumption")]
    public double P_Load { get; init; }

    /// <summary>Photovoltaic production power (W).</summary>
    [Description("Photovoltaic electrical power production")]
    public double P_PV { get; init; }

    /// <summary>UTC timestamp of the last reading.</summary>
    public DateTimeOffset? ReadingUtc { get; init; }

    /// <summary>
    /// Generate a <see cref="TableEntity" /> from this <see cref="FroniusSnapshotEntity" />.
    /// </summary>
    public TableEntity GetEntity()
    {
        var entity = new TableEntity(PartitionKey, RowKey)
        {
            { nameof(SOC), SOC },
            { nameof(P_Akku), P_Akku },
            { nameof(P_Grid), P_Grid },
            { nameof(P_Load), P_Load },
            { nameof(P_PV), P_PV },
            { nameof(ReadingUtc), ReadingUtc },
        };
        return entity;
    }
}
