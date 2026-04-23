namespace CasCap.Models;

/// <summary>
/// Azure Table Storage line-item entity that records individual edge hardware readings.
/// Uses ultra-short column names to minimize payload size for high-volume data.
/// </summary>
public class EdgeHardwareReadingEntity : ITableEntity
{
    /// <summary>Parameterless constructor required by Azure Table Storage SDK.</summary>
    public EdgeHardwareReadingEntity() { }

    /// <summary>
    /// Creates a new reading entity from an <see cref="EdgeHardwareEvent"/>.
    /// </summary>
    public EdgeHardwareReadingEntity(EdgeHardwareEvent evt)
    {
        PartitionKey = evt.NodeName;
        RowKey = evt.TimestampUtc.Ticks.ToString();

        n = evt.NodeName;
        pw = evt.GpuPowerDrawW;
        gt = evt.GpuTemperatureC;
        gu = evt.GpuUtilizationPercent;
        mu = evt.GpuMemoryUtilizationPercent;
        mm = evt.GpuMemoryUsedMiB;
        mt = evt.GpuMemoryTotalMiB;
        ct = evt.CpuTemperatureC;
    }

    /// <inheritdoc />
    public string PartitionKey { get; set; } = default!;

    /// <inheritdoc />
    public string RowKey { get; set; } = default!;

    /// <inheritdoc />
    public DateTimeOffset? Timestamp { get; set; }

    /// <inheritdoc />
    public ETag ETag { get; set; }

    /// <inheritdoc cref="EdgeHardwareEvent.NodeName"/>
    public string? n { get; init; }

    /// <inheritdoc cref="EdgeHardwareEvent.GpuPowerDrawW"/>
    public double? pw { get; init; }

    /// <inheritdoc cref="EdgeHardwareEvent.GpuTemperatureC"/>
    public double? gt { get; init; }

    /// <inheritdoc cref="EdgeHardwareEvent.GpuUtilizationPercent"/>
    public double? gu { get; init; }

    /// <inheritdoc cref="EdgeHardwareEvent.GpuMemoryUtilizationPercent"/>
    public double? mu { get; init; }

    /// <inheritdoc cref="EdgeHardwareEvent.GpuMemoryUsedMiB"/>
    public double? mm { get; init; }

    /// <inheritdoc cref="EdgeHardwareEvent.GpuMemoryTotalMiB"/>
    public double? mt { get; init; }

    /// <inheritdoc cref="EdgeHardwareEvent.CpuTemperatureC"/>
    public double? ct { get; init; }

    // ── Full-name accessors ──────────────────────────────────────────

    /// <summary>Kubernetes node name.</summary>
    public string? NodeName => n;

    /// <summary>GPU power draw in watts.</summary>
    public double? GpuPowerDrawW => pw;

    /// <summary>GPU temperature in degrees Celsius.</summary>
    public double? GpuTemperatureC => gt;

    /// <summary>GPU compute utilization percentage (0–100).</summary>
    public double? GpuUtilizationPercent => gu;

    /// <summary>GPU memory utilization percentage (0–100).</summary>
    public double? GpuMemoryUtilizationPercent => mu;

    /// <summary>GPU memory used in MiB.</summary>
    public double? GpuMemoryUsedMiB => mm;

    /// <summary>Total GPU memory in MiB.</summary>
    public double? GpuMemoryTotalMiB => mt;

    /// <summary>CPU temperature in degrees Celsius.</summary>
    public double? CpuTemperatureC => ct;

    /// <summary>Reconstructed UTC timestamp from the <see cref="RowKey"/>.</summary>
    public DateTime TimestampUtc => new(long.Parse(RowKey), DateTimeKind.Utc);

    /// <summary>
    /// Generate a <see cref="TableEntity"/> from this <see cref="EdgeHardwareReadingEntity"/>.
    /// </summary>
    public TableEntity GetEntity()
    {
        var entity = new TableEntity(PartitionKey, RowKey);
        if (n is not null) entity[nameof(n)] = n;
        if (pw.HasValue) entity[nameof(pw)] = pw.Value;
        if (gt.HasValue) entity[nameof(gt)] = gt.Value;
        if (gu.HasValue) entity[nameof(gu)] = gu.Value;
        if (mu.HasValue) entity[nameof(mu)] = mu.Value;
        if (mm.HasValue) entity[nameof(mm)] = mm.Value;
        if (mt.HasValue) entity[nameof(mt)] = mt.Value;
        if (ct.HasValue) entity[nameof(ct)] = ct.Value;
        return entity;
    }
}
