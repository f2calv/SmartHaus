namespace CasCap.Models;

/// <summary>
/// Azure Table Storage entity that records every individual Wiz bulb reading.
/// Uses ultra-short column names to reduce payload size for high-volume data.
/// </summary>
public class WizReadingEntity : ITableEntity
{
    /// <summary>Parameterless constructor required by Azure Table Storage SDK.</summary>
    public WizReadingEntity() { }

    /// <summary>Initializes a new instance from a <see cref="WizEvent"/>.</summary>
    public WizReadingEntity(WizEvent evt)
    {
        PartitionKey = evt.DeviceId;
        RowKey = evt.TimestampUtc.Ticks.ToString();

        ip = evt.IpAddress;
        s = evt.State;
        d = evt.Dimming;
        sc = evt.SceneId;
        t = evt.Temp;
        r = evt.Rssi;
    }

    /// <inheritdoc />
    public string PartitionKey { get; set; } = default!;

    /// <inheritdoc />
    public string RowKey { get; set; } = default!;

    /// <inheritdoc />
    public DateTimeOffset? Timestamp { get; set; }

    /// <inheritdoc />
    public ETag ETag { get; set; }

    /// <inheritdoc cref="WizEvent.IpAddress"/>
    public string ip { get; init; } = default!;

    /// <inheritdoc cref="WizEvent.State"/>
    public bool s { get; init; }

    /// <inheritdoc cref="WizEvent.Dimming"/>
    public int? d { get; init; }

    /// <inheritdoc cref="WizEvent.SceneId"/>
    public int? sc { get; init; }

    /// <inheritdoc cref="WizEvent.Temp"/>
    public int? t { get; init; }

    /// <inheritdoc cref="WizEvent.Rssi"/>
    public int? r { get; init; }

    // ── Full-name accessors ──────────────────────────────────────────

    /// <summary>Bulb IP address on the local network.</summary>
    public string IpAddress => ip;

    /// <summary>Whether the bulb is currently on.</summary>
    public bool State => s;

    /// <summary>Dimming level (10–100), null when off.</summary>
    public int? Dimming => d;

    /// <summary>Active scene ID, null if no scene.</summary>
    public int? SceneId => sc;

    /// <summary>Colour temperature in Kelvin.</summary>
    public int? Temp => t;

    /// <summary>Wi-Fi RSSI in dBm.</summary>
    public int? Rssi => r;

    /// <summary>Reconstructed UTC timestamp from the <see cref="RowKey"/>.</summary>
    public DateTime TimestampUtc => new(long.Parse(RowKey), DateTimeKind.Utc);

    /// <summary>Generates a <see cref="TableEntity"/> from this instance.</summary>
    public TableEntity GetEntity()
    {
        var entity = new TableEntity(PartitionKey, RowKey)
        {
            { nameof(ip), ip },
            { nameof(s), s },
        };
        if (d is not null) entity.Add(nameof(d), d);
        if (sc is not null) entity.Add(nameof(sc), sc);
        if (t is not null) entity.Add(nameof(t), t);
        if (r is not null) entity.Add(nameof(r), r);
        return entity;
    }
}
