namespace CasCap.Services;

/// <summary>Writes <see cref="CommsEvent"/> entries to the Redis Stream consumed by <see cref="CommunicationsBgService"/>.</summary>
/// <remarks>
/// Uses <see cref="IRemoteCache.Db"/> to call <c>XADD</c> on <see cref="CommsConfig.StreamKey"/>. Producers in
/// any pod write here; only the <c>Comms</c> feature holds the Signalizr connection.
/// </remarks>
[SinkType("CommsStream")]
public sealed partial class CommsStreamSinkService(
    ILogger<CommsStreamSinkService> logger,
    IOptions<CommsConfig> commsConfig,
    TimeProvider timeProvider,
    IHostEnvironment env,
    IRemoteCache remoteCache) : IEventSink<CommsEvent>
{
    //Stream timestamps are written as round-trip UTC; parsing without these styles converts them to local time.
    private const DateTimeStyles UtcTimestampStyles = DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal;

    /// <inheritdoc/>
    public string SinkType => "CommsStream";

    /// <inheritdoc/>
    public async Task WriteEvent(CommsEvent @event, CancellationToken cancellationToken = default)
    {
        var fields = new NameValueEntry[]
        {
            new(nameof(CommsEvent.Source), @event.Source),
            new(nameof(CommsEvent.Message), @event.Message),
            new(nameof(CommsEvent.TimestampUtc), @event.TimestampUtc.ToString("o")),
            new(nameof(CommsEvent.Environment), @event.Environment),
        };

        if (@event.JsonPayload is not null)
            fields = [.. fields, new(nameof(CommsEvent.JsonPayload), @event.JsonPayload)];

        var streamKey = commsConfig.Value.StreamKey;
        var entryId = await remoteCache.Db.StreamAddAsync(streamKey, fields);
        LogCommsEventWritten(logger, nameof(CommsStreamSinkService), entryId, @event.Source, streamKey);
    }

    /// <summary>Retrieves recent events from the Redis Stream backing this sink.</summary>
    public async IAsyncEnumerable<CommsEvent> GetEvents(string? id = null, int limit = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var entries = await remoteCache.Db.StreamRangeAsync(commsConfig.Value.StreamKey, count: limit);
        foreach (var entry in entries)
        {
            var dict = entry.Values.ToDictionary(v => v.Name.ToString(), v => v.Value.ToString());
            yield return new CommsEvent
            {
                Source = dict.GetValueOrDefault(nameof(CommsEvent.Source)) ?? "Unknown",
                Message = dict.GetValueOrDefault(nameof(CommsEvent.Message)) ?? string.Empty,
                Environment = dict.GetValueOrDefault(nameof(CommsEvent.Environment)) ?? env.GetAcronym(),
                TimestampUtc = DateTime.TryParse(dict.GetValueOrDefault(nameof(CommsEvent.TimestampUtc)), CultureInfo.InvariantCulture,
                    UtcTimestampStyles, out var ts)
                    ? ts
                    : timeProvider.GetUtcNow().UtcDateTime,
                JsonPayload = dict.GetValueOrDefault(nameof(CommsEvent.JsonPayload)),
            };
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} wrote event {EntryId} from {Source} to stream {StreamKey}")]
    private static partial void LogCommsEventWritten(ILogger logger, string className, RedisValue entryId, string source, string streamKey);
}
