using System.Collections.Concurrent;

namespace CasCap.Tests.Unit;

/// <summary>Recording <see cref="IEventSink{T}"/> substitute for comms events.</summary>
public sealed class FakeEventSink : IEventSink<CommsEvent>
{
    /// <inheritdoc/>
    public string SinkType => nameof(FakeEventSink);

    /// <summary>Every event written to the sink, in order.</summary>
    public ConcurrentQueue<CommsEvent> Events { get; } = new();

    /// <inheritdoc/>
    public Task WriteEvent(CommsEvent @event, CancellationToken cancellationToken = default)
    {
        Events.Enqueue(@event);
        return Task.CompletedTask;
    }
}
