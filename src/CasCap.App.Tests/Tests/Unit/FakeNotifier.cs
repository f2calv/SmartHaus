using System.Collections.Concurrent;
using System.Threading.Channels;

namespace CasCap.Tests.Unit;

/// <summary>
/// Deterministic <see cref="INotifier"/> substitute that feeds inbound envelopes to the comms
/// receive loop and records everything the service sends back.
/// </summary>
/// <remarks>
/// <see cref="ReceiveAsync"/> blocks until a batch is queued or the caller cancels, matching the
/// JSON-RPC transport the service is configured with in these tests.
/// </remarks>
public sealed class FakeNotifier : INotifier
{
    private readonly Channel<IReceivedNotification[]> _inbox =
        Channel.CreateUnbounded<IReceivedNotification[]>();

    private TaskCompletionSource _startProcessingGate = CompletedGate();

    /// <summary>One recorded reaction sent through <see cref="SendProgressUpdateAsync"/>.</summary>
    public sealed record Reaction(string Recipient, string Emoji, string TargetAuthor, long Timestamp);

    /// <summary>Groups returned by <see cref="ListGroupsAsync"/>.</summary>
    public List<INotificationGroup> Groups { get; } = [];

    /// <summary>Every message the service sent, in order.</summary>
    public ConcurrentQueue<INotificationMessage> Sent { get; } = new();

    /// <summary>Every reaction the service sent, in order.</summary>
    public ConcurrentQueue<Reaction> Reactions { get; } = new();

    /// <summary>Every attachment identifier the service downloaded, in order.</summary>
    public ConcurrentQueue<string> AttachmentFetches { get; } = new();

    /// <summary>Number of times the receive loop asked for envelopes.</summary>
    public int ReceiveCallCount => _receiveCallCount;
    private int _receiveCallCount;

    /// <summary>Number of times the group list was requested.</summary>
    public int ListGroupsCallCount => _listGroupsCallCount;
    private int _listGroupsCallCount;

    /// <summary>Number of times the reply drain loop started processing an accepted reply.</summary>
    public int StartProcessingCallCount => _startProcessingCallCount;
    private int _startProcessingCallCount;

    /// <summary>Bytes returned for any attachment download; <see langword="null"/> mimics a missing attachment.</summary>
    public byte[]? AttachmentContent { get; set; } = [0x01, 0x02, 0x03];

    /// <summary>Queues one batch of envelopes for the next <see cref="ReceiveAsync"/> call.</summary>
    public void Enqueue(params IReceivedNotification[] envelopes) => _inbox.Writer.TryWrite(envelopes);

    /// <summary>Counts reactions carrying the supplied emoji.</summary>
    public int ReactionCount(string emoji) => Reactions.Count(r => r.Emoji == emoji);

    /// <summary>Holds the reply drain loop inside <see cref="StartProcessingAsync"/> until released.</summary>
    /// <remarks>Used to fill the bounded reply queue and observe producer backpressure.</remarks>
    public void BlockStartProcessing() =>
        _startProcessingGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Releases any drain loop held by <see cref="BlockStartProcessing"/>.</summary>
    public void ReleaseStartProcessing() => _startProcessingGate.TrySetResult();

    /// <inheritdoc/>
    public Task<INotificationResponse?> SendAsync(INotificationMessage message, CancellationToken cancellationToken = default)
    {
        Sent.Enqueue(message);
        return Task.FromResult<INotificationResponse?>(new FakeNotificationResponse { Timestamp = "1" });
    }

    /// <inheritdoc/>
    public async Task<IReceivedNotification[]?> ReceiveAsync(string account, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _receiveCallCount);
        return _inbox.Reader.TryRead(out var batch)
            ? batch
            : await _inbox.Reader.ReadAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<byte[]?> GetAttachmentAsync(string attachmentId, CancellationToken cancellationToken = default)
    {
        AttachmentFetches.Enqueue(attachmentId);
        return Task.FromResult(AttachmentContent);
    }

    /// <inheritdoc/>
    public Task<INotificationGroup[]?> ListGroupsAsync(string account, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _listGroupsCallCount);
        return Task.FromResult<INotificationGroup[]?>([.. Groups]);
    }

    /// <inheritdoc/>
    public async Task<bool> StartProcessingAsync(string account, string recipient, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _startProcessingCallCount);
        await _startProcessingGate.Task;
        return true;
    }

    /// <inheritdoc/>
    public Task<bool> StopProcessingAsync(string account, string recipient, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    /// <inheritdoc/>
    public Task<bool> SendProgressUpdateAsync(string account, string recipient, string reaction, string targetAuthor,
        long timestamp, CancellationToken cancellationToken = default)
    {
        Reactions.Enqueue(new Reaction(recipient, reaction, targetAuthor, timestamp));
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<bool> UpdateProfileNameAsync(string account, string displayName, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    private static TaskCompletionSource CompletedGate()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        gate.SetResult();
        return gate;
    }
}
