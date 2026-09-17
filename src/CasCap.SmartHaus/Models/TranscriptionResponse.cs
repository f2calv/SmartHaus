using System.Text.Json.Serialization;

namespace CasCap.Models;

/// <summary>The JSON body returned by the openai-whisper-asr-webservice <c>/asr</c> route.</summary>
/// <remarks>Only the top-level <c>text</c> property is consumed; any additional fields are ignored.</remarks>
public sealed record TranscriptionResponse
{
    /// <summary>The transcribed text.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }
}
