namespace CasCap.Models;

/// <summary>
/// Represents a point-in-time snapshot of edge hardware telemetry (GPU and/or CPU).
/// Emitted by <see cref="CasCap.Services.EdgeHardwareMonitorBgService"/> and consumed by
/// <see cref="CasCap.Abstractions.IEventSink{T}"/> implementations.
/// </summary>
public record EdgeHardwareEvent
{
    /// <summary>Kubernetes node name (or machine name) that produced this reading.</summary>
    public required string NodeName { get; init; }

    /// <summary>UTC timestamp when this reading was captured.</summary>
    public DateTime TimestampUtc { get; init; }

    /// <summary>GPU power draw in watts, or <see langword="null"/> when GPU monitoring is unavailable.</summary>
    public double? GpuPowerDrawW { get; init; }

    /// <summary>GPU core temperature in degrees Celsius, or <see langword="null"/> when GPU monitoring is unavailable.</summary>
    public double? GpuTemperatureC { get; init; }

    /// <summary>GPU compute utilization as a percentage (0–100), or <see langword="null"/> when GPU monitoring is unavailable.</summary>
    public double? GpuUtilizationPercent { get; init; }

    /// <summary>GPU memory utilization as a percentage (0–100), or <see langword="null"/> when GPU monitoring is unavailable.</summary>
    public double? GpuMemoryUtilizationPercent { get; init; }

    /// <summary>GPU memory currently in use in MiB, or <see langword="null"/> when GPU monitoring is unavailable.</summary>
    public double? GpuMemoryUsedMiB { get; init; }

    /// <summary>Total GPU memory in MiB, or <see langword="null"/> when GPU monitoring is unavailable.</summary>
    public double? GpuMemoryTotalMiB { get; init; }

    /// <summary>CPU temperature in degrees Celsius, or <see langword="null"/> when CPU monitoring is unavailable.</summary>
    public double? CpuTemperatureC { get; init; }
}
