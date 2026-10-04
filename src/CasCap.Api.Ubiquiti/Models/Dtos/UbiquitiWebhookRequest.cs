using System.Text.Json.Serialization;

namespace CasCap.Models.Dtos;

/// <summary>UniFi Protect Alarm Manager webhook payload.</summary>
public sealed record UbiquitiWebhookRequest
{
    /// <summary>Alarm metadata and optional thumbnail.</summary>
    /// <example>{"name":"Person","triggers":[{"key":"person","device":"CAMERA_ID"}]}</example>
    [JsonPropertyName("alarm")]
    public AlarmData? Alarm { get; init; }

    /// <summary>Event timestamp in Unix milliseconds.</summary>
    /// <example>1790991000000</example>
    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; init; }

    /// <summary>Alarm metadata supplied by UniFi Protect.</summary>
    public sealed record AlarmData
    {
        /// <summary>Configured Alarm Manager name.</summary>
        /// <example>Person</example>
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        /// <summary>Optional JPEG thumbnail as raw base64 or a data URI.</summary>
        /// <example>data:image/jpeg;base64,/9j/...</example>
        [JsonPropertyName("thumbnail")]
        public string? Thumbnail { get; init; }

        /// <summary>Camera triggers that caused the alarm.</summary>
        /// <example>[{"key":"person","device":"CAMERA_ID"}]</example>
        [JsonPropertyName("triggers")]
        public IReadOnlyList<TriggerData> Triggers { get; init; } = [];
    }

    /// <summary>Individual camera trigger metadata.</summary>
    public sealed record TriggerData
    {
        /// <summary>Detection key reported by UniFi Protect.</summary>
        /// <example>person</example>
        [JsonPropertyName("key")]
        public string? Key { get; init; }

        /// <summary>Identifier of the camera that produced the event.</summary>
        /// <example>CAMERA_ID</example>
        [JsonPropertyName("device")]
        public string? Device { get; init; }
    }
}
