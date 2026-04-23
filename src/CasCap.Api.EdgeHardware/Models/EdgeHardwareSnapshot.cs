using System.Globalization;

namespace CasCap.Models;

/// <summary>
/// A point-in-time snapshot of edge hardware metrics — GPU telemetry (optional, from
/// <c>nvidia-smi</c>) and CPU temperature (always available on supported platforms).
/// </summary>
public record EdgeHardwareSnapshot
{
    // ── GPU metrics (optional — populated only when nvidia-smi is available) ──

    /// <summary>Whether this node has an NVIDIA GPU (i.e. <c>nvidia-smi</c> returned data).</summary>
    /// <remarks>Computed from <see cref="GpuMemoryTotalMiB"/>; not stored.</remarks>
    [Description("True when the node has an NVIDIA GPU, false when no GPU is present. When false all Gpu* fields are null.")]
    public bool HasGpu => GpuMemoryTotalMiB.HasValue;

    /// <summary>Instantaneous GPU power draw in watts, or <see langword="null"/> when GPU monitoring is unavailable.</summary>
    [Description("GPU power draw in watts, null when no GPU.")]
    public double? GpuPowerDrawW { get; init; }

    /// <summary>GPU core temperature in degrees Celsius, or <see langword="null"/> when GPU monitoring is unavailable.</summary>
    [Description("GPU temperature in Celsius, null when no GPU.")]
    public double? GpuTemperatureC { get; init; }

    /// <summary>GPU compute utilization as a percentage (0–100), or <see langword="null"/> when GPU monitoring is unavailable.</summary>
    [Description("GPU compute utilization percentage (0–100), null when no GPU.")]
    public double? GpuUtilizationPercent { get; init; }

    /// <summary>GPU memory utilization as a percentage (0–100), or <see langword="null"/> when GPU monitoring is unavailable.</summary>
    [Description("GPU memory utilization percentage (0–100), null when no GPU.")]
    public double? GpuMemoryUtilizationPercent { get; init; }

    /// <summary>GPU memory currently in use in MiB, or <see langword="null"/> when GPU monitoring is unavailable.</summary>
    [Description("GPU memory used in MiB, null when no GPU.")]
    public double? GpuMemoryUsedMiB { get; init; }

    /// <summary>Total GPU memory in MiB, or <see langword="null"/> when GPU monitoring is unavailable.</summary>
    [Description("Total GPU memory in MiB, null when no GPU.")]
    public double? GpuMemoryTotalMiB { get; init; }

    // ── CPU metrics (always available on supported platforms) ──────────

    /// <summary>CPU temperature in degrees Celsius, or <see langword="null"/> when CPU monitoring is unavailable.</summary>
    [Description("CPU temperature in Celsius.")]
    public double? CpuTemperatureC { get; init; }

    // ── Metadata ──────────────────────────────────────────────────────

    /// <summary>Kubernetes node name (or machine name) that produced this snapshot, or <see langword="null"/> when unknown.</summary>
    [Description("Kubernetes node / machine name that produced this snapshot.")]
    public string? NodeName { get; init; }

    /// <summary>UTC timestamp when this snapshot was captured.</summary>
    [Description("UTC timestamp of the snapshot.")]
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Parses the CSV output line from <c>nvidia-smi</c> into a GPU-only <see cref="EdgeHardwareSnapshot"/>.
    /// </summary>
    /// <remarks>
    /// Expected format: <c>power.draw, temperature.gpu, utilization.gpu, utilization.memory, memory.used, memory.total</c>.
    /// CPU temperature is not included in the nvidia-smi output; callers should set
    /// <see cref="CpuTemperatureC"/> separately.
    /// </remarks>
    public static EdgeHardwareSnapshot? ParseGpuCsv(string csv)
    {
        // nvidia-smi outputs "75.23, 62, 85, 44, 4096, 8192" (spaces after commas)
        var parts = csv.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length < 6)
            return null;

        if (!double.TryParse(parts[0], CultureInfo.InvariantCulture, out var powerW)
            || !double.TryParse(parts[1], CultureInfo.InvariantCulture, out var tempC)
            || !double.TryParse(parts[2], CultureInfo.InvariantCulture, out var utilGpu)
            || !double.TryParse(parts[3], CultureInfo.InvariantCulture, out _) // utilization.memory (bus activity, not allocation %)
            || !double.TryParse(parts[4], CultureInfo.InvariantCulture, out var memUsed)
            || !double.TryParse(parts[5], CultureInfo.InvariantCulture, out var memTotal))
            return null;

        return new EdgeHardwareSnapshot
        {
            GpuPowerDrawW = powerW,
            GpuTemperatureC = tempC,
            GpuUtilizationPercent = utilGpu,
            GpuMemoryUtilizationPercent = memTotal > 0 ? Math.Round(memUsed / memTotal * 100, 1) : 0,
            GpuMemoryUsedMiB = memUsed,
            GpuMemoryTotalMiB = memTotal,
            Timestamp = DateTimeOffset.UtcNow,
        };
    }
}
