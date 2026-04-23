namespace CasCap.Models;

/// <summary>
/// Azure Table Storage entity that records every individual Shelly smart plug reading.
/// Uses ultra-short column names to reduce payload size for high-volume data.
/// </summary>
public class ShellyReadingEntity : ITableEntity
{
    /// <summary>Initializes a new instance of the <see cref="ShellyReadingEntity"/> class.</summary>
    public ShellyReadingEntity() { }

    /// <summary>Initializes a new instance from a <see cref="ShellyEvent"/>.</summary>
    public ShellyReadingEntity(ShellyEvent evt)
    {
        PartitionKey = evt.DeviceId;
        RowKey = evt.TimestampUtc.Ticks.ToString();

        d = evt.DeviceId;
        p = evt.Power;
        r = evt.RelayState;
        t = evt.Temperature;
        o = evt.Overpower;
    }

    /// <inheritdoc />
    public string PartitionKey { get; set; } = default!;

    /// <inheritdoc />
    public string RowKey { get; set; } = default!;

    /// <inheritdoc />
    public DateTimeOffset? Timestamp { get; set; }

    /// <inheritdoc />
    public ETag ETag { get; set; }

    /// <inheritdoc cref="DeviceId"/>
    public string d { get; init; } = default!;

    /// <inheritdoc cref="Power"/>
    public double p { get; init; }

    /// <inheritdoc cref="RelayState"/>
    public double r { get; init; }

    /// <inheritdoc cref="Temperature"/>
    public double t { get; init; }

    /// <inheritdoc cref="Overpower"/>
    public double o { get; init; }

    /// <summary>The Shelly device ID.</summary>
    public string DeviceId { get { return d; } }

    /// <summary>Instantaneous power consumption in Watts.</summary>
    public double Power { get { return p; } }

    /// <summary>Relay state (1 = on, 0 = off).</summary>
    public double RelayState { get { return r; } }

    /// <summary>Device temperature in Celsius.</summary>
    public double Temperature { get { return t; } }

    /// <summary>Overpower condition (1 = triggered, 0 = normal).</summary>
    public double Overpower { get { return o; } }

    /// <summary>UTC timestamp parsed from RowKey.</summary>
    public DateTime TimestampUtc { get { return new DateTime(long.Parse(RowKey), DateTimeKind.Utc); } }

    /// <summary>
    /// Generate a <see cref="TableEntity" /> from this <see cref="ShellyReadingEntity" />.
    /// </summary>
    public TableEntity GetEntity()
    {
        var entity = new TableEntity(PartitionKey, RowKey)
        {
            { nameof(d), d },
            { nameof(p), p },
            { nameof(r), r },
            { nameof(t), t },
            { nameof(o), o },
        };
        return entity;
    }
}
