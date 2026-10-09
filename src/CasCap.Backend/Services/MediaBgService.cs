using CasCap.AgentRuntime.Client.Abstractions;
using CasCap.AgentRuntime.Contracts.V1;
using CasCap.Models;
using StackExchange.Redis;

namespace CasCap.Services;

/// <summary>
/// Single-instance background service (<c>Media</c> feature) that consumes
/// <see cref="MediaEvent"/> entries from the <see cref="MediaConfig.StreamKey"/>
/// Redis Stream, fetches the cached media bytes, and routes analysis to the domain
/// agent configured in <see cref="MediaConfig.SourceAgentMap"/>.
/// </summary>
/// <remarks>
/// <para>
/// Supports three media types: <see cref="MediaType.Image"/> (passed as binary to a
/// vision-capable agent), <see cref="MediaType.Audio"/> (passed as binary), and
/// <see cref="MediaType.Document"/> (text extracted via UglyToad.PdfPig before prompting).
/// </para>
/// <para>
/// Analysis results are posted back to the comms stream so the communications agent
/// can relay them to the notification group.
/// </para>
/// </remarks>
public sealed class MediaBgService(ILogger<MediaBgService> logger,
    IOptions<MediaConfig> mediaConfig,
    IHostEnvironment env,
    TimeProvider timeProvider,
    IRemoteCache remoteCache,
    IEventSink<CommsEvent> commsSink,
    IAgentRuntimeClient agentRuntimeClient) : IBgFeature
{
    private readonly IDatabase _db = remoteCache.Db;

    //Stream timestamps are written as round-trip UTC; parsing without these styles converts them to local time.
    private const System.Globalization.DateTimeStyles UtcTimestampStyles =
        System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal;

    /// <inheritdoc/>
    public string FeatureName => FeatureNames.Comms;

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{ClassName} starting, sourceAgentMap={SourceAgentMap}",
            nameof(MediaBgService),
            string.Join(", ", mediaConfig.Value.SourceAgentMap.Select(kv => $"{kv.Key}→{kv.Value}")));

        await EnsureConsumerGroupAsync();
        await DrainStreamAsync(cancellationToken);

        logger.LogInformation("{ClassName} exiting", nameof(MediaBgService));
    }

    private async Task EnsureConsumerGroupAsync()
    {
        try
        {
            await _db.StreamCreateConsumerGroupAsync(
                mediaConfig.Value.StreamKey,
                mediaConfig.Value.ConsumerGroup,
                mediaConfig.Value.ConsumerGroupStartId,
                createStream: true);
            logger.LogInformation("{ClassName} created consumer group {ConsumerGroup} on stream {StreamKey}",
                nameof(MediaBgService), mediaConfig.Value.ConsumerGroup, mediaConfig.Value.StreamKey);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            logger.LogDebug("{ClassName} consumer group {ConsumerGroup} already exists",
                nameof(MediaBgService), mediaConfig.Value.ConsumerGroup);
        }
    }

    private async Task DrainStreamAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{ClassName} consuming stream {StreamKey} as {ConsumerGroup}/{ConsumerName}",
            nameof(MediaBgService), mediaConfig.Value.StreamKey,
            mediaConfig.Value.ConsumerGroup, mediaConfig.Value.ConsumerName);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var entries = await _db.StreamReadGroupAsync(
                    mediaConfig.Value.StreamKey,
                    mediaConfig.Value.ConsumerGroup,
                    mediaConfig.Value.ConsumerName,
                    mediaConfig.Value.StreamReadPosition,
                    count: mediaConfig.Value.StreamReadCount);

                if (entries.Length > 0)
                {
                    foreach (var entry in entries)
                    {
                        var mediaEvent = DeserializeStreamEntry(entry);
                        await ProcessMediaEventAsync(mediaEvent, cancellationToken);
                        await _db.StreamAcknowledgeAsync(
                            mediaConfig.Value.StreamKey,
                            mediaConfig.Value.ConsumerGroup,
                            entry.Id);
                    }
                }
                else
                    await Task.Delay(TimeSpan.FromMilliseconds(mediaConfig.Value.PollingIntervalMs), cancellationToken);
            }
            catch (RedisServerException ex) when (ex.Message.Contains("NOGROUP"))
            {
                logger.LogWarning("{ClassName} consumer group disappeared, recreating", nameof(MediaBgService));
                await EnsureConsumerGroupAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
            {
                logger.LogError(ex, "{ClassName} error during stream read cycle", nameof(MediaBgService));
                await Task.Delay(TimeSpan.FromMilliseconds(mediaConfig.Value.PollingIntervalMs), cancellationToken);
            }
        }
    }

    private async Task ProcessMediaEventAsync(MediaEvent mediaEvent, CancellationToken cancellationToken)
    {
        logger.LogInformation("{ClassName} processing {MediaType} event from {Source} ({EventType})",
            nameof(MediaBgService), mediaEvent.MediaType, mediaEvent.Source, mediaEvent.EventType);

        if (!mediaConfig.Value.SourceAgentMap.TryGetValue(mediaEvent.Source, out var agentKey))
        {
            logger.LogWarning("{ClassName} no agent mapped for source {Source}, skipping",
                nameof(MediaBgService), mediaEvent.Source);
            return;
        }

        // Fetch cached media bytes from Redis.
        var mediaBytes = (byte[]?)await _db.StringGetAsync(mediaEvent.Media.MediaRedisKey);
        if (mediaBytes is null || mediaBytes.Length == 0)
        {
            logger.LogWarning("{ClassName} media not found at {MediaRedisKey}",
                nameof(MediaBgService), mediaEvent.Media.MediaRedisKey);
            return;
        }

        logger.LogInformation("{ClassName} running {AgentKey} on {Size} byte {MediaType} from {Source}",
            nameof(MediaBgService), agentKey, mediaBytes.Length, mediaEvent.MediaType, mediaEvent.Source);

        try
        {
            var result = await agentRuntimeClient.RunAgentAsync(
                agentKey,
                CreateRunRequest(mediaEvent, mediaBytes),
                cancellationToken);

            if (result is null)
            {
                logger.LogWarning("{ClassName} remote agent {AgentKey} is unavailable for {Source} {MediaType}",
                    nameof(MediaBgService), agentKey, mediaEvent.Source, mediaEvent.MediaType);
                await _db.KeyDeleteAsync(mediaEvent.Media.MediaRedisKey, CommandFlags.FireAndForget);
                return;
            }

            logger.LogInformation("{ClassName} {AgentKey} completed in {Duration}",
                nameof(MediaBgService), agentKey, TimeSpan.FromMilliseconds(result.ElapsedMilliseconds));

            // Post findings back to the comms stream.
            if (!string.IsNullOrWhiteSpace(result.OutputText))
            {
                var mediaPayload = new MediaReference
                {
                    MediaRedisKey = mediaEvent.Media.MediaRedisKey,
                    MimeType = mediaEvent.Media.MimeType ?? "image/jpeg",
                    FileName = $"{mediaEvent.Source}_{mediaEvent.EventType}.jpg",
                };
                var findingsEvent = new CommsEvent
                {
                    Source = nameof(MediaBgService),
                    Message = $"{mediaEvent.Source} {mediaEvent.MediaType} analysis ({mediaEvent.EventType}): {result.OutputText}",
                    Environment = env.GetAcronym(),
                    TimestampUtc = timeProvider.GetUtcNow().UtcDateTime,
                    JsonPayload = mediaPayload.ToJson(),
                };
                await commsSink.WriteEvent(findingsEvent, cancellationToken);
            }
            else
            {
                // No findings — clean up cached media immediately.
                await _db.KeyDeleteAsync(mediaEvent.Media.MediaRedisKey, CommandFlags.FireAndForget);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{ClassName} {AgentKey} analysis failed for {Source} {MediaType}",
                nameof(MediaBgService), agentKey, mediaEvent.Source, mediaEvent.MediaType);
            // Analysis failed — clean up cached media.
            await _db.KeyDeleteAsync(mediaEvent.Media.MediaRedisKey, CommandFlags.FireAndForget);
        }
    }

    internal static RunAgentRequest CreateRunRequest(MediaEvent mediaEvent, byte[] mediaBytes)
    {
        var (input, binaryContent, mimeType) = mediaEvent.MediaType switch
        {
            MediaType.Image => (
                $"Analyze this image captured by {mediaEvent.Source} for the {mediaEvent.EventType} event. "
                    + "Describe concisely who or what is present and any relevant security finding.",
                mediaBytes,
                mediaEvent.Media.MimeType ?? "image/jpeg"),
            MediaType.Audio => (
                $"Analyze this audio captured by {mediaEvent.Source} for the {mediaEvent.EventType} event. "
                    + "Describe concisely the relevant sounds or speech.",
                mediaBytes,
                mediaEvent.Media.MimeType ?? "audio/wav"),
            MediaType.Document => (ExtractDocumentText(mediaBytes), null, null),
            _ => (
                $"Analyze this media captured by {mediaEvent.Source} for the {mediaEvent.EventType} event.",
                mediaBytes,
                mediaEvent.Media.MimeType ?? "application/octet-stream"),
        };

        return new RunAgentRequest
        {
            SessionId = $"media-{Guid.NewGuid():N}",
            Input = input,
            BinaryContent = binaryContent,
            MimeType = mimeType,
            BypassSession = true,
        };
    }

    /// <summary>
    /// Extracts text content from a PDF document byte array.
    /// </summary>
    /// <remarks>Future: integrate UglyToad.PdfPig for PDF text extraction.</remarks>
    private static string ExtractDocumentText(byte[] documentBytes)
    {
        //TODO: integrate UglyToad.PdfPig for PDF text extraction
        // using var document = UglyToad.PdfPig.PdfDocument.Open(documentBytes);
        // var text = string.Join("\n", document.GetPages().Select(p => p.Text));
        return $"[Document received, {documentBytes.Length} bytes — PDF text extraction not yet implemented]";
    }

    private MediaEvent DeserializeStreamEntry(StreamEntry entry)
    {
        var dict = entry.Values.ToDictionary(v => v.Name.ToString(), v => v.Value.ToString());
        return new MediaEvent
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
            TimestampUtc = DateTime.TryParse(dict.GetValueOrDefault(nameof(MediaEvent.TimestampUtc)), System.Globalization.CultureInfo.InvariantCulture, UtcTimestampStyles, out var ts)
                ? ts
                : timeProvider.GetUtcNow().UtcDateTime,
            Metadata = dict.GetValueOrDefault(nameof(MediaEvent.Metadata)),
        };
    }
}
