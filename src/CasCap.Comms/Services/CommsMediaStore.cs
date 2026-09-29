namespace CasCap.Services;

/// <summary>Caches comms attachment bytes in Redis and returns the <see cref="MediaReference"/> a producer puts in its event.</summary>
/// <remarks>
/// Producers and the <see cref="CommunicationsBgService"/> consumer can run in different pods, so an
/// attachment travels through Redis rather than a shared filesystem. The consumer deletes the key once
/// the attachment is sent, and <see cref="CommsConfig.MediaCacheTtlMs"/> removes any left unsent.
/// </remarks>
public sealed partial class CommsMediaStore(
    ILogger<CommsMediaStore> logger,
    IOptions<CommsConfig> commsConfig,
    TimeProvider timeProvider,
    IRemoteCache remoteCache)
{
    /// <summary>Caches <paramref name="content"/> and returns a reference to it.</summary>
    /// <param name="content">The attachment bytes.</param>
    /// <param name="mimeType">The media type of <paramref name="content"/>, for example <c>"image/png"</c>.</param>
    /// <param name="fileName">The file name shown for the attachment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The reference to serialise into <see cref="CommsEvent.JsonPayload"/>.</returns>
    public async Task<MediaReference> StoreAsync(byte[] content, string mimeType, string fileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = $"{commsConfig.Value.MediaCacheKeyPrefix}:{Guid.CreateVersion7(timeProvider.GetUtcNow()):N}";
        await remoteCache.Db.StringSetAsync(key, content, TimeSpan.FromMilliseconds(commsConfig.Value.MediaCacheTtlMs));
        LogMediaCached(logger, nameof(CommsMediaStore), content.Length, key);
        return new MediaReference { MediaRedisKey = key, MimeType = mimeType, FileName = fileName };
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "{ClassName} cached {Size} byte attachment as {MediaRedisKey}")]
    private static partial void LogMediaCached(ILogger logger, string className, int size, string mediaRedisKey);
}
