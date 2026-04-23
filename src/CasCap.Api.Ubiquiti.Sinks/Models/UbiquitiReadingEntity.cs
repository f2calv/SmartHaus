namespace CasCap.Models;

/// <summary>
/// Azure Table Storage entity that records every individual Ubiquiti camera event.
/// </summary>
public class UbiquitiReadingEntity : ITableEntity
{
    /// <summary>
    /// Parameterless constructor required by Azure Table Storage deserialization.
    /// </summary>
    public UbiquitiReadingEntity() { }

    /// <summary>
    /// Creates a new reading entity from an <see cref="UbiquitiEvent"/>.
    /// </summary>
    public UbiquitiReadingEntity(UbiquitiEvent evt)
    {
        PartitionKey = evt.CameraId ?? "unknown";
        RowKey = evt.DateCreatedUtc.Ticks.ToString();
        t = evt.UbiquitiEventType.ToString();
        c = evt.CameraId;
        n = evt.CameraName;
    }

    /// <inheritdoc />
    public string PartitionKey { get; set; } = default!;

    /// <inheritdoc />
    public string RowKey { get; set; } = default!;

    /// <inheritdoc />
    public DateTimeOffset? Timestamp { get; set; }

    /// <inheritdoc />
    public ETag ETag { get; set; }

    /// <inheritdoc cref="EventType"/>
    public string t { get; init; } = default!;

    /// <inheritdoc cref="CameraId"/>
    public string? c { get; init; }

    /// <inheritdoc cref="CameraName"/>
    public string? n { get; init; }

    /// <summary>
    /// The string representation of the <see cref="UbiquitiEventType"/>.
    /// </summary>
    public string EventType => t;

    /// <summary>
    /// The camera identifier that produced the event.
    /// </summary>
    public string? CameraId => c;

    /// <summary>
    /// The display name of the camera that produced the event.
    /// </summary>
    public string? CameraName => n;

    /// <summary>
    /// The UTC date/time reconstructed from the <see cref="RowKey"/> ticks.
    /// </summary>
    public DateTime TimestampUtc => new(long.Parse(RowKey), DateTimeKind.Utc);

    /// <summary>
    /// Generates a <see cref="TableEntity"/> from this <see cref="UbiquitiReadingEntity"/>.
    /// </summary>
    public TableEntity GetEntity()
    {
        var entity = new TableEntity(PartitionKey, RowKey)
        {
            { nameof(t), t },
        };
        if (c is not null)
            entity[nameof(c)] = c;
        if (n is not null)
            entity[nameof(n)] = n;
        return entity;
    }
}
