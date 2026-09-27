using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using CasCap.Signalizr.Client;

namespace CasCap.Tests.Unit;

/// <summary>
/// Deterministic Signalizr client that feeds deliveries to the comms subscription and records
/// every group operation the service performs.
/// </summary>
public sealed class FakeSignalizrClient : ISignalizrClient
{
    private readonly Channel<SignalizrMessage> _inbox = Channel.CreateUnbounded<SignalizrMessage>();

    private TaskCompletionSource _startTypingGate = CompletedGate();

    /// <summary>One recorded reaction set through <see cref="SetReactionAsync"/>.</summary>
    public sealed record Reaction(string GroupName, string Emoji, long TargetTimestamp, string? TargetAuthor);

    /// <summary>Configured groups returned to the service during startup.</summary>
    public IReadOnlyList<string> Groups { get; set; } = [CommunicationsBgServiceTestFixture.ChatGroupName];

    /// <summary>Messages sent through the gateway.</summary>
    public ConcurrentQueue<(string GroupName, string Message, IReadOnlyList<string>? Attachments)> Sent { get; } = new();

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

    /// <summary>Messages sent to one group, in order.</summary>
    public IEnumerable<(string GroupName, string Message, IReadOnlyList<string>? Attachments)> SentTo(string groupName) =>
        Sent.Where(sent => sent.GroupName == groupName);

    /// <summary>Holds the reply drain loop inside <see cref="StartTypingAsync"/> until released.</summary>
    /// <remarks>Used to fill the bounded reply queue and observe producer backpressure.</remarks>
    public void BlockStartTyping() =>
        _startTypingGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Releases any drain loop held by <see cref="BlockStartTyping"/>.</summary>
    public void ReleaseStartTyping() => _startTypingGate.TrySetResult();

    /// <inheritdoc/>
    public Task<string> SendAsync(string groupName, string message,
        CancellationToken cancellationToken = default) =>
        SendAsync(groupName, message, base64Attachments: null, cancellationToken);

    /// <inheritdoc/>
    public Task<string> SendAsync(string groupName, string message,
        IReadOnlyList<string>? base64Attachments, CancellationToken cancellationToken = default)
    {
        Sent.Enqueue((groupName, message, base64Attachments));
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
    public Task<IReadOnlyList<string>> GetGroupsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Groups);

    /// <inheritdoc/>
    public Task SetReactionAsync(string groupName, string reaction, long targetTimestamp,
        string? targetAuthor = null, CancellationToken cancellationToken = default)
    {
        Reactions.Enqueue(new Reaction(groupName, reaction, targetTimestamp, targetAuthor));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RemoveReactionAsync(string groupName, string reaction, long targetTimestamp,
        string? targetAuthor = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task SetReactionAsync(SignalizrMessage message, string reaction, CancellationToken cancellationToken = default) =>
        SetReactionAsync(message.GroupName!, reaction, message.Timestamp, message.Sender, cancellationToken);

    /// <inheritdoc/>
    public Task RemoveReactionAsync(SignalizrMessage message, string reaction, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc/>
    public async Task StartTypingAsync(string groupName, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _startTypingCallCount);
        await _startTypingGate.Task;
    }

    /// <inheritdoc/>
    public Task StopTypingAsync(string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task<string> CreatePollAsync(string groupName, string question, IReadOnlyList<string> answers,
        bool allowMultipleSelections = false, CancellationToken cancellationToken = default) =>
        Task.FromResult(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());

    /// <inheritdoc/>
    public Task ClosePollAsync(string groupName, string pollId, CancellationToken cancellationToken = default) =>
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
