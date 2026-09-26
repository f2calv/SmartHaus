using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using CasCap.Signalizr.Client;

namespace CasCap.Tests.Unit;

/// <summary>
/// Deterministic Signalizr client that feeds deliveries to the comms subscription and records
/// every channel operation the service performs.
/// </summary>
public sealed class FakeSignalizrClient : ISignalizrClient
{
    private readonly Channel<SignalizrMessage> _inbox = Channel.CreateUnbounded<SignalizrMessage>();

    private TaskCompletionSource _startTypingGate = CompletedGate();

    /// <summary>One recorded reaction set through <see cref="SetReactionAsync"/>.</summary>
    public sealed record Reaction(string Channel, string Emoji, long TargetTimestamp, string? TargetAuthor);

    /// <summary>Configured channels returned to the service during startup.</summary>
    public IReadOnlyList<string> Channels { get; set; } = ["smarthaus.chat"];

    /// <summary>Messages sent through the gateway.</summary>
    public ConcurrentQueue<(string Channel, string Message, IReadOnlyList<string>? Attachments)> Sent { get; } = new();

    /// <summary>Every reaction the service set, in order.</summary>
    public ConcurrentQueue<Reaction> Reactions { get; } = new();

    /// <summary>Every durable attachment identifier requested by the service.</summary>
    public ConcurrentQueue<string> AttachmentFetches { get; } = new();

    /// <summary>Durable attachment bytes keyed by Signalizr attachment identifier.</summary>
    public ConcurrentDictionary<string, byte[]> Attachments { get; } = new();

    /// <summary>Number of subscriptions opened by the service.</summary>
    public int SubscribeCallCount => _subscribeCallCount;
    private int _subscribeCallCount;

    /// <summary>Number of times the reply drain loop showed the typing indicator for an accepted reply.</summary>
    public int StartTypingCallCount => _startTypingCallCount;
    private int _startTypingCallCount;

    /// <summary>Adds an inbound durable delivery.</summary>
    public void Enqueue(SignalizrMessage message) => _inbox.Writer.TryWrite(message);

    /// <summary>Counts reactions carrying the supplied emoji.</summary>
    public int ReactionCount(string emoji) => Reactions.Count(r => r.Emoji == emoji);

    /// <summary>Messages sent to one channel, in order.</summary>
    public IEnumerable<(string Channel, string Message, IReadOnlyList<string>? Attachments)> SentTo(string channel) =>
        Sent.Where(sent => sent.Channel == channel);

    /// <summary>Holds the reply drain loop inside <see cref="StartTypingAsync"/> until released.</summary>
    /// <remarks>Used to fill the bounded reply queue and observe producer backpressure.</remarks>
    public void BlockStartTyping() =>
        _startTypingGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Releases any drain loop held by <see cref="BlockStartTyping"/>.</summary>
    public void ReleaseStartTyping() => _startTypingGate.TrySetResult();

    /// <inheritdoc/>
    public Task<string> SendAsync(string channel, string message,
        CancellationToken cancellationToken = default) =>
        SendAsync(channel, message, base64Attachments: null, cancellationToken);

    /// <inheritdoc/>
    public Task<string> SendAsync(string channel, string message,
        IReadOnlyList<string>? base64Attachments, CancellationToken cancellationToken = default)
    {
        Sent.Enqueue((channel, message, base64Attachments));
        return Task.FromResult(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
    }

    /// <inheritdoc/>
    public Task<byte[]> GetAttachmentAsync(string attachmentId,
        CancellationToken cancellationToken = default)
    {
        AttachmentFetches.Enqueue(attachmentId);
        return Task.FromResult(Attachments[attachmentId]);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> GetChannelsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Channels);

    /// <inheritdoc/>
    public Task SetReactionAsync(string channel, string reaction, long targetTimestamp,
        string? targetAuthor = null, CancellationToken cancellationToken = default)
    {
        Reactions.Enqueue(new Reaction(channel, reaction, targetTimestamp, targetAuthor));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RemoveReactionAsync(string channel, string reaction, long targetTimestamp,
        string? targetAuthor = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task SetReactionAsync(SignalizrMessage message, string reaction, CancellationToken cancellationToken = default) =>
        SetReactionAsync(message.Channel!, reaction, message.Timestamp, message.Sender, cancellationToken);

    /// <inheritdoc/>
    public Task RemoveReactionAsync(SignalizrMessage message, string reaction, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc/>
    public async Task StartTypingAsync(string channel, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _startTypingCallCount);
        await _startTypingGate.Task;
    }

    /// <inheritdoc/>
    public Task StopTypingAsync(string channel, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task<string> CreatePollAsync(string channel, string question, IReadOnlyList<string> answers,
        bool allowMultipleSelections = false, CancellationToken cancellationToken = default) =>
        Task.FromResult(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());

    /// <inheritdoc/>
    public Task ClosePollAsync(string channel, string pollId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc/>
    public async IAsyncEnumerable<SignalizrMessage> SubscribeAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _subscribeCallCount);
        await foreach (var message in _inbox.Reader.ReadAllAsync(cancellationToken))
            yield return message;
    }

    private static TaskCompletionSource CompletedGate()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        gate.SetResult();
        return gate;
    }
}
