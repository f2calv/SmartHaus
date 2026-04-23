using System.Text.Json.Serialization;

namespace CasCap.Models;

/// <summary>Represents the current open/closed state of the front door contact sensor.</summary>
public record FrontDoorContactState
{
    /// <summary>Whether the front door is currently physically open.</summary>
    [Description("true = door is physically open, false = door is physically closed. This is a contact sensor — it does NOT indicate locked/unlocked.")]
    public bool IsOpen { get; init; }

    /// <summary>Human-readable state description.</summary>
    [Description("Physical state of the door: 'Open' or 'Closed'. NOT locked/unlocked.")]
    public required string State { get; init; }

    /// <summary>When the contact sensor state was last updated (UTC).</summary>
    [Description("UTC timestamp of the last state update.")]
    public DateTime LastUpdated { get; init; }

    /// <summary>The underlying KNX binary-input contact data.</summary>
    [Description("Raw KNX contact data including group name, floor, room, and decoded state.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public KnxContact? Contact { get; init; }
}
