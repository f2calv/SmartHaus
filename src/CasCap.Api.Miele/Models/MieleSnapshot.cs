namespace CasCap.Models;

/// <summary>A point-in-time snapshot of a Miele appliance state.</summary>
public record MieleSnapshot
{
    /// <summary>Device identifier.</summary>
    [Description("Miele device identifier.")]
    public string? DeviceId { get; init; }

    /// <summary>Appliance type or name.</summary>
    [Description("Appliance type or name.")]
    public string? DeviceName { get; init; }

    /// <summary>Last known status code.</summary>
    [Description("Miele status code.")]
    public int? StatusCode { get; init; }

    /// <summary>Last known program ID.</summary>
    [Description("Active program ID.")]
    public int? ProgramId { get; init; }

    /// <summary>Last known error code.</summary>
    [Description("Last error code, 0 means no error.")]
    public int? ErrorCode { get; init; }

    /// <summary>UTC timestamp of the last reading.</summary>
    [Description("UTC timestamp of the snapshot.")]
    public DateTimeOffset? ReadingUtc { get; init; }
}
