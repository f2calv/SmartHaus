using StackExchange.Redis;
using System.Runtime.CompilerServices;

namespace CasCap.Services;

/// <summary>
/// Writes <see cref="CommsEvent"/> entries to a Redis Stream so that the
/// <c>Comms</c> feature instance can consume them for agent-driven
/// decision making.
/// </summary>
/// <remarks>
/// Uses <see cref="IRemoteCache.Db"/> to call <c>XADD</c>. The stream key is
/// <see cref="CommsAgentConfig.StreamKey"/>.
/// </remarks>
public class CommsStreamSinkService(ILogger<CommsStreamSinkService> logger,
    IOptions<CommsAgentConfig> commsAgentConfig,
    IRemoteCache remoteCache) : IEventSink<CommsEvent>
{
    private readonly IDatabase _db = remoteCache.Db;

    /// <inheritdoc/>
    public async Task WriteEvent(CommsEvent @event, CancellationToken cancellationToken = default)
    {
        var fields = new NameValueEntry[]
        {
            new(nameof(CommsEvent.Source), @event.Source),
            new(nameof(CommsEvent.Message), @event.Message),
            new(nameof(CommsEvent.TimestampUtc), @event.TimestampUtc.ToString("o")),
        };

        if (@event.JsonPayload is not null)
            fields = [.. fields, new(nameof(CommsEvent.JsonPayload), @event.JsonPayload)];

        var streamKey = commsAgentConfig.Value.StreamKey;
        var entryId = await _db.StreamAddAsync(streamKey, fields);
        logger.LogDebug("{ClassName} wrote event {EntryId} from {Source} to stream {StreamKey}",
            nameof(CommsStreamSinkService), entryId, @event.Source, streamKey);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<CommsEvent> GetEvents(string? id = null, int limit = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var entries = await _db.StreamRangeAsync(commsAgentConfig.Value.StreamKey, count: limit);
        foreach (var entry in entries)
        {
            var dict = entry.Values.ToDictionary(v => v.Name.ToString(), v => v.Value.ToString());
            yield return new CommsEvent
            {
                Source = dict.GetValueOrDefault(nameof(CommsEvent.Source)) ?? "Unknown",
                Message = dict.GetValueOrDefault(nameof(CommsEvent.Message)) ?? string.Empty,
                TimestampUtc = DateTime.TryParse(dict.GetValueOrDefault(nameof(CommsEvent.TimestampUtc)), out var ts)
                    ? ts
                    : DateTime.UtcNow,
                JsonPayload = dict.GetValueOrDefault(nameof(CommsEvent.JsonPayload)),
            };
        }
    }
}
