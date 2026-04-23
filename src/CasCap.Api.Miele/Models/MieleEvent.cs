namespace CasCap.Models;

/// <summary>Represents a Miele appliance event received via the SSE stream.</summary>
public record MieleEvent
{
    /// <summary>Miele device identifier.</summary>
    [Description("Miele device identifier")]
    public required string DeviceId { get; init; }

    /// <summary>Appliance type or name.</summary>
    [Description("Appliance type or name")]
    public string? DeviceName { get; init; }

    /// <summary>The type of event.</summary>
    [Description("Event type: StatusUpdate, ProgramStarted, ProgramComplete, Error, ConnectionLost")]
    public MieleEventType EventType { get; init; }

    /// <summary>Device status code from the Miele API.</summary>
    [Description("Miele status code")]
    public int? StatusCode { get; init; }

    /// <summary>Program identifier, if applicable.</summary>
    [Description("Active program ID")]
    public int? ProgramId { get; init; }

    /// <summary>Program name, if applicable.</summary>
    [Description("Active program name")]
    public string? ProgramName { get; init; }

    /// <summary>Error code, if the event represents a fault.</summary>
    [Description("Miele error code, 0 means no error")]
    public int? ErrorCode { get; init; }

    /// <summary>Raw JSON payload from the SSE stream.</summary>
    public string? RawJson { get; init; }

    /// <summary>UTC timestamp of the event.</summary>
    public DateTime TimestampUtc { get; init; }
}
