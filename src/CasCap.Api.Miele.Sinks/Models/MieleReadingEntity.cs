namespace CasCap.Models;

/// <summary>
/// Azure Table Storage entity that records every individual Miele appliance event.
/// Uses ultra-short column names to reduce payload size for high-volume data.
/// </summary>
public class MieleReadingEntity : ITableEntity
{
    /// <summary>Parameterless constructor required by Azure Table Storage SDK.</summary>
    public MieleReadingEntity() { }

    /// <summary>Initializes a new instance from a <see cref="MieleEvent"/>.</summary>
    public MieleReadingEntity(MieleEvent evt)
    {
        PartitionKey = evt.DeviceId;
        RowKey = evt.TimestampUtc.Ticks.ToString();

        did = evt.DeviceId;
        et = (int)evt.EventType;
        sc = evt.StatusCode;
        pid = evt.ProgramId;
        ec = evt.ErrorCode;
    }

    /// <inheritdoc />
    public string PartitionKey { get; set; } = default!;

    /// <inheritdoc />
    public string RowKey { get; set; } = default!;

    /// <inheritdoc />
    public DateTimeOffset? Timestamp { get; set; }

    /// <inheritdoc />
    public ETag ETag { get; set; }

    /// <inheritdoc cref="MieleEvent.DeviceId"/>
    public string did { get; init; } = default!;

    /// <inheritdoc cref="MieleEvent.EventType"/>
    public int et { get; init; }

    /// <inheritdoc cref="MieleEvent.StatusCode"/>
    public int? sc { get; init; }

    /// <inheritdoc cref="MieleEvent.ProgramId"/>
    public int? pid { get; init; }

    /// <inheritdoc cref="MieleEvent.ErrorCode"/>
    public int? ec { get; init; }

    // ── Full-name accessors ──────────────────────────────────────────

    /// <summary>Appliance device identifier.</summary>
    public string DeviceId => did;

    /// <summary>Event type as a <see cref="MieleEventType"/> value.</summary>
    public MieleEventType EventType => (MieleEventType)et;

    /// <summary>Appliance status code.</summary>
    public int? StatusCode => sc;

    /// <summary>Program identifier.</summary>
    public int? ProgramId => pid;

    /// <summary>Error code.</summary>
    public int? ErrorCode => ec;

    /// <summary>Reconstructed UTC timestamp from the <see cref="RowKey"/>.</summary>
    public DateTime TimestampUtc => new(long.Parse(RowKey), DateTimeKind.Utc);

    /// <summary>Generates a <see cref="TableEntity"/> from this instance.</summary>
    public TableEntity GetEntity()
    {
        var entity = new TableEntity(PartitionKey, RowKey)
        {
            { nameof(did), did },
            { nameof(et), et },
        };
        if (sc is not null) entity.Add(nameof(sc), sc);
        if (pid is not null) entity.Add(nameof(pid), pid);
        if (ec is not null) entity.Add(nameof(ec), ec);
        return entity;
    }
}
