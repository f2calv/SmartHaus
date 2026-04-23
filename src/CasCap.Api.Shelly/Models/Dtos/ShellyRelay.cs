using System.Text.Json.Serialization;

namespace CasCap.Models.Dtos;

/// <summary>
/// Relay state within a Shelly device status.
/// </summary>
public record ShellyRelay
{
    /// <summary>Whether the relay is switched on.</summary>
    [JsonPropertyName("ison")]
    public bool IsOn { get; init; }

    /// <summary>Whether a timer is running on the relay.</summary>
    [JsonPropertyName("has_timer")]
    public bool HasTimer { get; init; }

    /// <summary>Remaining timer duration in seconds.</summary>
    [JsonPropertyName("timer_remaining")]
    public int TimerRemaining { get; init; }

    /// <summary>Whether the relay has been overloaded.</summary>
    [JsonPropertyName("overpower")]
    public bool Overpower { get; init; }
}
