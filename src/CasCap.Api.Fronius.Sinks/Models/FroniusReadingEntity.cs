namespace CasCap.Models;

/// <summary>
/// Azure Table Storage entity that records every individual Fronius inverter reading.
/// Uses ultra-short column names to reduce payload size for high-volume data.
/// </summary>
public class FroniusReadingEntity : ITableEntity
{
    /// <summary>Initializes a new instance of the <see cref="FroniusReadingEntity"/> class.</summary>
    public FroniusReadingEntity() { }

    /// <summary>Initializes a new instance from a <see cref="FroniusEvent"/>.</summary>
    public FroniusReadingEntity(FroniusEvent evt)
    {
        PartitionKey = evt.TimestampUtc.ToString("yyMMdd");
        RowKey = evt.TimestampUtc.Ticks.ToString();

        s = evt.SOC;
        a = evt.P_Akku;
        g = evt.P_Grid;
        l = evt.P_Load;
        p = evt.P_PV;
    }

    /// <inheritdoc />
    public string PartitionKey { get; set; } = default!;

    /// <inheritdoc />
    public string RowKey { get; set; } = default!;

    /// <inheritdoc />
    public DateTimeOffset? Timestamp { get; set; }

    /// <inheritdoc />
    public ETag ETag { get; set; }

    /// <inheritdoc cref="SOC"/>
    public double s { get; init; }

    /// <inheritdoc cref="P_Akku"/>
    public double a { get; init; }

    /// <inheritdoc cref="P_Grid"/>
    public double g { get; init; }

    /// <inheritdoc cref="P_Load"/>
    public double l { get; init; }

    /// <inheritdoc cref="P_PV"/>
    public double p { get; init; }

    /// <summary>
    /// SOC / State Of Charge
    /// </summary>
    public double SOC { get { return s; } }

    /// <summary>
    /// P_Akku / Battery
    /// </summary>
    public double P_Akku { get { return a; } }

    /// <summary>
    /// P_Grid / Power From Grid
    /// </summary>
    public double P_Grid { get { return g; } }

    /// <summary>
    /// P_Load / Power Load
    /// </summary>
    public double P_Load { get { return l; } }

    /// <summary>
    /// P_PV / Photovoltaic Power
    /// </summary>
    public double P_PV { get { return p; } }

    /// <summary>UTC timestamp parsed from RowKey.</summary>
    public DateTime TimestampUtc { get { return new DateTime(long.Parse(RowKey), DateTimeKind.Utc); } }

    /// <summary>
    /// Generate a <see cref="TableEntity" /> from this <see cref="FroniusReadingEntity" />.
    /// </summary>
    public TableEntity GetEntity()
    {
        var entity = new TableEntity(PartitionKey, RowKey)
        {
            { nameof(s), s },
            { nameof(a), a },
            { nameof(g), g },
            { nameof(l), l },
            { nameof(p), p },
        };
        return entity;
    }
}
