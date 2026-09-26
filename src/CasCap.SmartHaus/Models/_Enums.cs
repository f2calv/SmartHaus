namespace CasCap.Models;

/// <summary>Identifies the type of media payload in a <see cref="MediaEvent"/>.</summary>
public enum MediaType
{
    /// <summary>Still image (JPEG, PNG, etc.).</summary>
    Image,

    /// <summary>Audio recording (WAV, MP3, etc.).</summary>
    Audio,

    /// <summary>Document (PDF, etc.).</summary>
    Document,
}
