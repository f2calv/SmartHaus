using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace CasCap.Tests.Unit;

//ISpeechToTextClient is published as experimental (MEAI001); see WhisperAsrSpeechToTextClient.
#pragma warning disable MEAI001

/// <summary>
/// An <see cref="ISpeechToTextClient"/> returning a canned transcript, so the comms orchestration
/// can be exercised without a speech-to-text backend.
/// </summary>
public sealed class FakeSpeechToTextClient : ISpeechToTextClient
{
    /// <summary>The transcript returned by <see cref="GetTextAsync"/>.</summary>
    public string Transcript { get; set; } = "transcribed text";

    /// <summary>When set, <see cref="GetTextAsync"/> throws this instead of returning.</summary>
    public Exception? Failure { get; set; }

    /// <summary>The number of transcription requests received.</summary>
    public int Requests { get; private set; }

    /// <summary>When set, <see cref="GetTextAsync"/> waits on it before returning.</summary>
    /// <remarks>Lets a test observe what happens while transcription is still in flight.</remarks>
    public TaskCompletionSource? Gate { get; set; }

    /// <inheritdoc/>
    public async Task<SpeechToTextResponse> GetTextAsync(Stream audioSpeechStream,
        SpeechToTextOptions? speechToTextOptions = null, CancellationToken cancellationToken = default)
    {
        Requests++;
        if (Gate is not null)
            await Gate.Task.WaitAsync(cancellationToken);
        if (Failure is not null)
            throw Failure;
        return new SpeechToTextResponse(Transcript);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<SpeechToTextResponseUpdate> GetStreamingTextAsync(Stream audioSpeechStream,
        SpeechToTextOptions? speechToTextOptions = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetTextAsync(audioSpeechStream, speechToTextOptions, cancellationToken);
        foreach (var update in response.ToSpeechToTextResponseUpdates())
            yield return update;
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    /// <inheritdoc/>
    public void Dispose() { }
}
