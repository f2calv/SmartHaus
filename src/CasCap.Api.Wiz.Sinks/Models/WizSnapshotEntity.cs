namespace CasCap.Models;

/// <summary>Azure Table Storage snapshot entity storing the latest Wiz bulb state.</summary>
public class WizSnapshotEntity : ITableEntity
{
    /// <summary>Parameterless constructor required by Azure Table Storage SDK.</summary>
    public WizSnapshotEntity() { }

    /// <summary>Initializes a new instance from a <see cref="WizEvent"/>.</summary>
    public WizSnapshotEntity(string partitionKey, WizEvent evt)
    {
        PartitionKey = partitionKey;
        RowKey = evt.DeviceId;

        DeviceId = evt.DeviceId;
        IpAddress = evt.IpAddress;
        Mac = evt.Mac;
        State = evt.State;
        Dimming = evt.Dimming;
        SceneId = evt.SceneId;
        Temp = evt.Temp;
        Rssi = evt.Rssi;
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

    /// <summary>Device identifier.</summary>
    public string DeviceId { get; init; } = default!;

    /// <summary>Bulb IP address.</summary>
    public string IpAddress { get; init; } = default!;

    /// <summary>Bulb MAC address.</summary>
    public string? Mac { get; init; }

    /// <summary>Whether the bulb is on.</summary>
    public bool State { get; init; }

    /// <summary>Dimming level.</summary>
    public int? Dimming { get; init; }

    /// <summary>Scene ID.</summary>
    public int? SceneId { get; init; }

    /// <summary>Colour temperature in Kelvin.</summary>
    public int? Temp { get; init; }

    /// <summary>Wi-Fi RSSI.</summary>
    public int? Rssi { get; init; }

    /// <summary>UTC timestamp of the last reading.</summary>
    public DateTimeOffset? ReadingUtc { get; init; }

    /// <summary>Generates a <see cref="TableEntity"/> from this instance.</summary>
    public TableEntity GetEntity()
    {
        var entity = new TableEntity(PartitionKey, RowKey)
        {
            { nameof(DeviceId), DeviceId },
            { nameof(IpAddress), IpAddress },
            { nameof(State), State },
            { nameof(ReadingUtc), ReadingUtc },
        };
        if (Mac is not null) entity.Add(nameof(Mac), Mac);
        if (Dimming is not null) entity.Add(nameof(Dimming), Dimming);
        if (SceneId is not null) entity.Add(nameof(SceneId), SceneId);
        if (Temp is not null) entity.Add(nameof(Temp), Temp);
        if (Rssi is not null) entity.Add(nameof(Rssi), Rssi);
        return entity;
    }
}
