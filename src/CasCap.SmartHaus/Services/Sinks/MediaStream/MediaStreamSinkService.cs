using StackExchange.Redis;
using System.Runtime.CompilerServices;

namespace CasCap.Services;

/// <summary>
/// Writes <see cref="MediaEvent"/> entries to the <see cref="MediaConfig.StreamKey"/>
/// Redis Stream for consumption by <see cref="MediaBgService"/>.
/// </summary>
public class MediaStreamSinkService(ILogger<MediaStreamSinkService> logger,
    IOptions<MediaConfig> mediaConfig,
    IRemoteCache remoteCache) : IEventSink<MediaEvent>
{
    private readonly IDatabase _db = remoteCache.Db;

    /// <inheritdoc/>
    public async Task WriteEvent(MediaEvent @event, CancellationToken cancellationToken = default)
    {
        var fields = new NameValueEntry[]
        {
            new(nameof(MediaEvent.Source), @event.Source),
            new(nameof(MediaEvent.EventType), @event.EventType),
            new(nameof(MediaReference.MediaRedisKey), @event.Media.MediaRedisKey),
            new(nameof(MediaEvent.MediaType), @event.MediaType.ToString()),
            new(nameof(MediaEvent.TimestampUtc), @event.TimestampUtc.ToString("o")),
        };

        if (@event.Media.MimeType is not null)
            fields = [.. fields, new(nameof(MediaReference.MimeType), @event.Media.MimeType)];

        if (@event.Metadata is not null)
            fields = [.. fields, new(nameof(MediaEvent.Metadata), @event.Metadata)];

        var streamKey = mediaConfig.Value.StreamKey;
        var entryId = await _db.StreamAddAsync(streamKey, fields);
        logger.LogDebug("{ClassName} wrote event {EntryId} from {Source} to stream {StreamKey}",
            nameof(MediaStreamSinkService), entryId, @event.Source, streamKey);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<MediaEvent> GetEvents(string? id = null, int limit = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var entries = await _db.StreamRangeAsync(mediaConfig.Value.StreamKey, count: limit);
        foreach (var entry in entries)
        {
            var dict = entry.Values.ToDictionary(v => v.Name.ToString(), v => v.Value.ToString());
            yield return new MediaEvent
            {
                Source = dict.GetValueOrDefault(nameof(MediaEvent.Source)) ?? "Unknown",
                EventType = dict.GetValueOrDefault(nameof(MediaEvent.EventType)) ?? string.Empty,
                Media = new MediaReference
                {
                    MediaRedisKey = dict.GetValueOrDefault(nameof(MediaReference.MediaRedisKey)) ?? string.Empty,
                    MimeType = dict.GetValueOrDefault(nameof(MediaReference.MimeType)),
                },
                MediaType = Enum.TryParse<MediaType>(dict.GetValueOrDefault(nameof(MediaEvent.MediaType)), out var mt)
                    ? mt
                    : MediaType.Image,
                TimestampUtc = DateTime.TryParse(dict.GetValueOrDefault(nameof(MediaEvent.TimestampUtc)), out var ts)
                    ? ts
                    : DateTime.UtcNow,
                Metadata = dict.GetValueOrDefault(nameof(MediaEvent.Metadata)),
            };
        }
    }
}
