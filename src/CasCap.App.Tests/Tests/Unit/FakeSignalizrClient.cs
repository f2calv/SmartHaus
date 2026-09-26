using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using CasCap.Signalizr.Client;

namespace CasCap.Tests.Unit;

/// <summary>Deterministic Signalizr client for communications orchestration tests.</summary>
public sealed class FakeSignalizrClient : ISignalizrClient
{
    private readonly Channel<SignalizrMessage> _inbox = Channel.CreateUnbounded<SignalizrMessage>();

    /// <summary>Configured channels returned to the service during startup.</summary>
    public IReadOnlyList<string> Channels { get; set; } = ["smarthaus.chat"];

    /// <summary>Messages sent through the gateway.</summary>
    public ConcurrentQueue<(string Channel, string Message, IReadOnlyList<string>? Attachments)> Sent { get; } = new();

    /// <summary>Every durable attachment identifier requested by the service.</summary>
    public ConcurrentQueue<string> AttachmentFetches { get; } = new();

    /// <summary>Durable attachment bytes keyed by Signalizr attachment identifier.</summary>
    public ConcurrentDictionary<string, byte[]> Attachments { get; } = new();

    /// <summary>Number of subscriptions opened by the service.</summary>
    public int SubscribeCallCount => _subscribeCallCount;
    private int _subscribeCallCount;

    /// <summary>Adds an inbound durable delivery.</summary>
    public void Enqueue(SignalizrMessage message) => _inbox.Writer.TryWrite(message);

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
    public async IAsyncEnumerable<SignalizrMessage> SubscribeAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _subscribeCallCount);
        await foreach (var message in _inbox.Reader.ReadAllAsync(cancellationToken))
            yield return message;
    }
}
