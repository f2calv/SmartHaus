namespace CasCap.Services;

/// <inheritdoc/>
public sealed class UbiquitiQueryService(
    ILogger<UbiquitiQueryService> logger,
    TimeProvider timeProvider,
    IEnumerable<IEventSink<UbiquitiEvent>> eventSinks,
    IUbiquitiQuery? ubiquitiQuery = null
    ) : IUbiquitiQueryService
{
    /// <inheritdoc/>
    public async Task<UbiquitiSnapshot> GetSnapshot()
    {
        if (ubiquitiQuery is null)
            return new UbiquitiSnapshot { SnapshotUtc = timeProvider.GetUtcNow().UtcDateTime };

        return await ubiquitiQuery.GetSnapshot().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SendAlert(
        UbiquitiEventType type,
        string? cameraId = null,
        string? cameraName = null,
        double? score = null,
        UbiquitiWebhookRequest? webhook = null,
        CancellationToken cancellationToken = default)
    {
        var (thumbnail, thumbnailMimeType) = DecodeThumbnail(webhook?.Alarm?.Thumbnail);
        var timestampUtc = GetTimestampUtc(webhook?.Timestamp);
        var webhookCameraIds = webhook?.Alarm?.Triggers
            .Select(trigger => trigger.Device)
            .Where(id => id is not null)
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];

        if (cameraId is not null || webhookCameraIds.Length == 0)
        {
            await DispatchAsync(cameraId, cancellationToken).ConfigureAwait(false);
            return;
        }

        foreach (var webhookCameraId in webhookCameraIds)
            await DispatchAsync(webhookCameraId, cancellationToken).ConfigureAwait(false);

        async Task DispatchAsync(string? sourceCameraId, CancellationToken token)
        {
            var eventCameraId = MaskCameraId(sourceCameraId);
            logger.LogInformation("{ClassName} sending alert for event type {EventType} from camera {CameraName}",
                nameof(UbiquitiQueryService), type, cameraName ?? eventCameraId ?? "unknown");

            var ubiquitiEvent = new UbiquitiEvent
            {
                UbiquitiEventType = type,
                DateCreatedUtc = timestampUtc,
                CameraId = eventCameraId,
                SourceCameraId = sourceCameraId,
                CameraName = cameraName,
                Score = score,
                Thumbnail = thumbnail,
                ThumbnailMimeType = thumbnailMimeType,
            };

            var sinkTasks = new List<Task>(eventSinks.Count());
            foreach (var eventSink in eventSinks)
                sinkTasks.Add(eventSink.WriteEvent(ubiquitiEvent, token));
            await Task.WhenAll(sinkTasks).ConfigureAwait(false);
        }
    }

    private static (byte[]? Bytes, string? MimeType) DecodeThumbnail(string? thumbnail)
    {
        if (string.IsNullOrWhiteSpace(thumbnail))
            return (null, null);

        const string DataPrefix = "data:";
        const string Base64Marker = ";base64";

        var encoded = thumbnail;
        var mimeType = "image/jpeg";
        if (thumbnail.StartsWith(DataPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var separatorIndex = thumbnail.IndexOf(',');
            if (separatorIndex < 0)
                throw new FormatException("The UniFi thumbnail data URI has no payload separator.");

            var metadata = thumbnail[DataPrefix.Length..separatorIndex];
            if (!metadata.EndsWith(Base64Marker, StringComparison.OrdinalIgnoreCase))
                throw new FormatException("The UniFi thumbnail data URI is not base64 encoded.");

            mimeType = metadata[..^Base64Marker.Length];
            if (!mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                throw new FormatException("The UniFi thumbnail MIME type is not an image.");

            encoded = thumbnail[(separatorIndex + 1)..];
        }

        return (Convert.FromBase64String(encoded), mimeType);
    }

    private DateTime GetTimestampUtc(long? timestamp)
    {
        if (timestamp is null)
            return timeProvider.GetUtcNow().UtcDateTime;

        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(timestamp.Value).UtcDateTime;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new FormatException("The UniFi webhook timestamp is outside the supported range.", ex);
        }
    }

    private static string? MaskCameraId(string? cameraId)
        => cameraId is { Length: > 4 }
            ? $"***{cameraId[^4..]}"
            : cameraId;
}
