namespace CasCap.Models;

/// <summary>
/// Azure Table Storage entity that records every individual Sicce device reading.
/// Uses ultra-short column names to reduce payload size for high-volume data.
/// </summary>
public class SicceReadingEntity : ITableEntity
{
    /// <summary>Parameterless constructor required by Azure Table Storage SDK.</summary>
    public SicceReadingEntity() { }

    /// <summary>Initializes a new instance from a <see cref="SicceEvent"/>.</summary>
    /// <param name="evt">The Sicce event to persist.</param>
    public SicceReadingEntity(SicceEvent evt)
    {
        PartitionKey = evt.TimestampUtc.ToString("yyMMdd");
        RowKey = evt.TimestampUtc.Ticks.ToString();

        t = evt.Temperature;
        p = evt.Power;
        o = evt.IsOnline;
        s = evt.PowerSwitch;
    }

    /// <inheritdoc />
    public string PartitionKey { get; set; } = default!;

    /// <inheritdoc />
    public string RowKey { get; set; } = default!;

    /// <inheritdoc />
    public DateTimeOffset? Timestamp { get; set; }

    /// <inheritdoc />
    public ETag ETag { get; set; }

    /// <inheritdoc cref="SicceEvent.Temperature"/>
    public double t { get; init; }

    /// <inheritdoc cref="SicceEvent.Power"/>
    public double p { get; init; }

    /// <inheritdoc cref="SicceEvent.IsOnline"/>
    public bool o { get; init; }

    /// <inheritdoc cref="SicceEvent.PowerSwitch"/>
    public bool s { get; init; }

    // ── Full-name accessors ──────────────────────────────────────────

    /// <summary>Device temperature in degrees Celsius.</summary>
    public double Temperature => t;

    /// <summary>Power level as a ratio (0.05–1.0).</summary>
    public double Power => p;

    /// <summary>Whether the device is online.</summary>
    public bool IsOnline => o;

    /// <summary>Whether the power switch is on.</summary>
    public bool PowerSwitch => s;

    /// <summary>Reconstructed UTC timestamp from the <see cref="RowKey"/>.</summary>
    public DateTime TimestampUtc => new(long.Parse(RowKey), DateTimeKind.Utc);

    /// <summary>
    /// Generate a <see cref="TableEntity"/> from this <see cref="SicceReadingEntity"/>.
    /// </summary>
    public TableEntity GetEntity()
    {
        var entity = new TableEntity(PartitionKey, RowKey)
        {
            { nameof(t), t },
            { nameof(p), p },
            { nameof(o), o },
            { nameof(s), s },
        };
        return entity;
    }
}
