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

/// <summary>Controls how far an inbound voice message is carried through the pipeline.</summary>
public enum VoiceProcessingMode
{
    /// <summary>Voice messages are rejected without transcription.</summary>
    Disabled,

    /// <summary>Voice messages are transcribed, but no agent turn or reply is produced.</summary>
    Shadow,

    /// <summary>Voice messages are transcribed and drive a normal text-only agent turn.</summary>
    Enabled,
}

/// <summary>The terminal outcome of a bounded voice-message transcription attempt.</summary>
public enum VoiceTranscriptionOutcome
{
    /// <summary>A normalised, non-empty transcript was produced.</summary>
    Success,

    /// <summary>The declared media type is not an accepted audio container.</summary>
    Unsupported,

    /// <summary>The compressed bytes, decoded bytes or decoded duration exceeded the configured limit.</summary>
    Oversized,

    /// <summary>The payload was empty, or its file signature contradicted the declared media type.</summary>
    Invalid,

    /// <summary>Normalisation to 16 kHz mono signed 16-bit PCM WAV failed.</summary>
    ConversionFailed,

    /// <summary>The configured time budget elapsed before a transcript was produced.</summary>
    TimedOut,

    /// <summary>The transcription backend returned a non-success response or was unreachable.</summary>
    BackendFailed,

    /// <summary>The backend responded successfully but produced no usable text.</summary>
    EmptyTranscript,
}
