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

/// <summary>Selects which speech-to-text backend transcribes an inbound voice message.</summary>
/// <remarks>
/// The three are interchangeable behind <c>ISpeechToTextClient</c>, so switching provider is a
/// configuration change rather than a code change. Measured throughput for each, and the problems
/// each one presented, are recorded in
/// <see href="https://github.com/f2calv/SmartHaus/issues/82">issue 82</see>.
/// </remarks>
public enum SpeechToTextProvider
{
    /// <summary>openai-whisper-asr-webservice, transcribing on the CPU.</summary>
    WhisperAsr,

    /// <summary>whisper.cpp <c>whisper-server</c>, able to offload to a GPU through Vulkan.</summary>
    WhisperCpp,

    /// <summary>The Azure AI Speech fast transcription API. The only provider that sends audio off the network.</summary>
    Azure,
}

/// <summary>Selects which text-to-speech backend synthesizes a spoken reply.</summary>
/// <remarks>
/// The backends are interchangeable behind <c>ITextToSpeechClient</c>, so switching provider is a
/// configuration change rather than a code change. The comparison that produced this list, and the
/// design decisions behind the adapters, are recorded in
/// <see href="https://github.com/f2calv/SmartHaus/issues/82">issue 82</see>.
/// </remarks>
public enum TextToSpeechProvider
{
    /// <summary>Azure AI Speech synthesis, the same resource and credential the transcription provider uses.</summary>
    AzureSpeech,

    /// <summary>An Azure OpenAI audio deployment, reached through the OpenAI-compatible speech route.</summary>
    AzureOpenAi,

    /// <summary>The Azure AI Speech neural synthesis container, run on our own hardware.</summary>
    /// <remarks>
    /// TODO: not implemented. The appeal is that text and audio never leave the network while the
    /// voices stay the cloud ones, unlike every other self-hosted option here.
    /// <para>
    /// Two things block it today. The image
    /// (<c>mcr.microsoft.com/azure-cognitive-services/speechservices/neural-text-to-speech</c>)
    /// publishes an amd64 manifest only, verified against the registry, so it cannot run on the arm64
    /// edge node; and access is gated behind a Microsoft approval request. Its appetite is the real
    /// obstacle though: Microsoft's own run command allocates 6 CPU cores and 12 GB of memory, against
    /// the 226 MB Piper was measured at. Each image tag also carries a single voice.
    /// </para>
    /// <para>
    /// It still meters usage back to the cloud resource, so it needs outbound connectivity and bills
    /// per character exactly as <see cref="AzureSpeech"/> does — it removes the data disclosure, not
    /// the dependency or the cost. The Speech SDK reaches it through a host rather than an endpoint
    /// and without a credential, so <c>SpeechService</c> needs a host construction path before the
    /// existing Azure adapter can serve it.
    /// </para>
    /// </remarks>
    AzureSpeechContainer,

    /// <summary>A self-hosted Piper server. Keeps synthesis on the local network.</summary>
    /// <remarks>
    /// TODO: not implemented, but measured and by far the lightest option
    /// (<c>lscr.io/linuxserver/piper</c>, linux/arm64): 43 MB resident idle, 226 MB with an
    /// <c>en_GB</c> voice loaded, synthesizing at about 6.4x realtime on CPU.
    /// <para>
    /// That image speaks the Wyoming protocol on TCP 10200, not HTTP: a newline-delimited JSON header
    /// declaring <c>data_length</c> and <c>payload_length</c>, followed by those bytes in turn. It
    /// needs a socket client rather than an <see cref="System.Net.Http.HttpClient"/>, and it emits raw
    /// PCM at 22.05 kHz, so its adapter must encode to Opus itself.
    /// </para>
    /// <para>
    /// Two ways to avoid the protocol work. Piper upstream ships an HTTP server module returning WAV,
    /// and <c>openedai-speech</c> wraps Piper behind an OpenAI-compatible route, which the existing
    /// OpenAI client could drive once its credential is made optional.
    /// </para>
    /// </remarks>
    Piper,

    /// <summary>A self-hosted Kokoro server. Keeps synthesis on the local network.</summary>
    /// <remarks>
    /// TODO: not implemented. Deploy <see href="https://github.com/remsky/Kokoro-FastAPI"/>
    /// (<c>ghcr.io/remsky/kokoro-fastapi-cpu</c>, linux/arm64). It exposes an OpenAI-compatible speech
    /// route, so the existing OpenAI client should serve it once the endpoint is configurable and the
    /// credential optional. Resident memory is unmeasured and is the number that matters: it carries a
    /// PyTorch runtime, so expect substantially more than Piper.
    /// </remarks>
    Kokoro,

    /// <summary>A self-hosted Coqui XTTS server, which can clone a voice from a short reference clip.</summary>
    /// <remarks>
    /// TODO: not implemented, and the only entry here chosen for a capability rather than for speed
    /// or footprint. Zero-shot cloning needs roughly six seconds of reference audio.
    /// <para>
    /// Three things need settling before it is worth building. The upstream
    /// <see href="https://github.com/coqui-ai/TTS"/> is archived, so the maintained fork
    /// <see href="https://github.com/idiap/coqui-ai-TTS"/> is the real target. The published images are
    /// built for amd64 and CUDA, so arm64 on the edge node is unproven and may mean building our own.
    /// The XTTS-v2 weights are released under the Coqui Public Model License, which is
    /// non-commercial, so check that before it becomes anything other than a household experiment.
    /// </para>
    /// <para>
    /// It emits WAV only, so its client must encode to Opus itself rather than asking the server for it.
    /// </para>
    /// </remarks>
    CoquiXtts,
}

/// <summary>Controls when a spoken reply is produced alongside the text one.</summary>
public enum VoiceReplyMode
{
    /// <summary>Replies are text only.</summary>
    Disabled,

    /// <summary>A spoken reply is produced only when the incoming message was itself a voice message.</summary>
    MatchInbound,

    /// <summary>Every reply is spoken, whatever the incoming message was.</summary>
    Always,
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
