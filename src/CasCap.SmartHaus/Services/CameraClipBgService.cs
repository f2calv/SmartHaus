namespace CasCap.Services;

/// <summary>
/// Consumes accepted camera events, retrieves bounded MediaMTX time ranges, remuxes them for
/// Signal compatibility, and publishes the resulting attachment to the communications stream.
/// </summary>
/// <param name="logger">Logger.</param>
/// <param name="config">Camera clip options.</param>
/// <param name="timeProvider">Time source used for post-roll and identifiers.</param>
/// <param name="env">Host environment used to label communications events.</param>
/// <param name="clipQueue">Bounded camera event queue.</param>
/// <param name="mediaPublisher">Thumbnail fallback publisher.</param>
/// <param name="httpClientFactory">Factory for the private playback client.</param>
/// <param name="mediaStore">Bounded Redis attachment store.</param>
/// <param name="commsSink">Communications stream sink.</param>
public sealed class CameraClipBgService(
    ILogger<CameraClipBgService> logger,
    IOptions<CameraClipConfig> config,
    TimeProvider timeProvider,
    IHostEnvironment env,
    CameraClipQueue clipQueue,
    CameraThumbnailPublisher mediaPublisher,
    IHttpClientFactory httpClientFactory,
    CommsMediaStore mediaStore,
    IEventSink<CommsEvent> commsSink) : BackgroundService
{
    private readonly HttpClient _playbackClient =
        httpClientFactory.CreateClient(nameof(CameraClipBgService));

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!config.Value.Enabled)
        {
            logger.LogInformation("{ClassName} is disabled", nameof(CameraClipBgService));
            await Task.Delay(Timeout.InfiniteTimeSpan, timeProvider, cancellationToken);
            return;
        }

        Directory.CreateDirectory(config.Value.WorkingDirectory);
        logger.LogInformation("{ClassName} started with {SourceCount} configured sources",
            nameof(CameraClipBgService),
            config.Value.Sources.Count + (config.Value.DoorBirdSource is null ? 0 : 1));

        await foreach (var request in clipQueue.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                await ProcessClip(request, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    "{ClassName} failed to produce a clip for {Camera}; ErrorType={ErrorType}",
                    nameof(CameraClipBgService),
                    request.Source.DisplayName,
                    ex.GetType().Name);
                await PublishFallback(request, cancellationToken);
            }
        }
    }

    private async Task ProcessClip(
        CameraClipRequest request,
        CancellationToken cancellationToken)
    {
        var postRollEndUtc = request.TimestampUtc
            .AddSeconds(config.Value.PostRollSeconds);
        var delay = postRollEndUtc - timeProvider.GetUtcNow().UtcDateTime;
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, timeProvider, cancellationToken);

        using var timeout = new CancellationTokenSource(
            TimeSpan.FromMilliseconds(config.Value.ProcessingTimeoutMs),
            timeProvider);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);

        var id = Guid.CreateVersion7(timeProvider.GetUtcNow());
        var sourceFile = Path.Combine(config.Value.WorkingDirectory, $"{id:N}.source.mp4");
        var outputFile = Path.Combine(config.Value.WorkingDirectory, $"{id:N}.signal.mp4");
        try
        {
            await DownloadClip(request, sourceFile, budget.Token);
            await RemuxClip(sourceFile, outputFile, budget.Token);

            var file = new FileInfo(outputFile);
            if (!file.Exists || file.Length == 0 || file.Length > config.Value.MaximumClipBytes)
                throw new InvalidDataException("Remuxed clip size is outside the configured bounds.");

            var bytes = await File.ReadAllBytesAsync(outputFile, budget.Token);
            var media = await mediaStore.StoreAsync(
                bytes,
                "video/mp4",
                "camera-event.mp4",
                budget.Token);

            await commsSink.WriteEvent(new CommsEvent
            {
                Source = nameof(CameraClipBgService),
                Message = $"Security camera {request.Source.DisplayName} detected "
                    + $"{request.EventType} at "
                    + $"{request.TimestampUtc:yyyy-MM-dd HH:mm:ss} UTC",
                Environment = env.GetAcronym(),
                TimestampUtc = timeProvider.GetUtcNow().UtcDateTime,
                JsonPayload = media.ToJson(),
            }, budget.Token);

            logger.LogInformation(
                "{ClassName} published a {Bytes} byte clip for {Camera}",
                nameof(CameraClipBgService),
                bytes.Length,
                request.Source.DisplayName);
        }
        finally
        {
            File.Delete(sourceFile);
            File.Delete(outputFile);
        }
    }

    private async Task DownloadClip(
        CameraClipRequest request,
        string destination,
        CancellationToken cancellationToken)
    {
        var start = new DateTimeOffset(
            request.TimestampUtc.AddSeconds(-config.Value.PreRollSeconds),
            TimeSpan.Zero);
        var duration = config.Value.PreRollSeconds + config.Value.PostRollSeconds;
        var requestUri = "get"
            + $"?path={Uri.EscapeDataString(request.Source.Path)}"
            + $"&start={Uri.EscapeDataString(start.ToString("o", CultureInfo.InvariantCulture))}"
            + $"&duration={duration.ToString(CultureInfo.InvariantCulture)}"
            + "&format=mp4";

        using var response = await _playbackClient.GetAsync(
            requestUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is > 0
            && response.Content.Headers.ContentLength > config.Value.MaximumClipBytes)
        {
            throw new InvalidDataException("Playback response exceeds the configured byte limit.");
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destinationStream = new FileStream(
            destination,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var buffer = new byte[64 * 1024];
        var total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
                break;

            total += read;
            if (total > config.Value.MaximumClipBytes)
                throw new InvalidDataException("Playback response exceeded the configured byte limit.");

            await destinationStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        if (total == 0)
            throw new InvalidDataException("Playback returned an empty clip.");
    }

    private async Task RemuxClip(
        string source,
        string destination,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(config.Value.FfmpegPath)
        {
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[]
        {
            "-nostdin", "-hide_banner", "-loglevel", "error", "-y",
            "-i", source,
            "-map", "0:v:0",
            "-map", "0:a:0?",
            "-c", "copy",
            "-movflags", "+faststart",
            destination,
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException("FFmpeg did not start.");

        try
        {
            var errorDrain = process.StandardError.BaseStream.CopyToAsync(
                Stream.Null,
                cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            await errorDrain;
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            throw;
        }

        if (process.ExitCode != 0)
            throw new InvalidDataException($"FFmpeg exited with code {process.ExitCode}.");
    }

    private async Task PublishFallback(
        CameraClipRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Thumbnail is not null)
        {
            await mediaPublisher.PublishThumbnail(request, cancellationToken);
            return;
        }

        await commsSink.WriteEvent(new CommsEvent
        {
            Source = nameof(CameraClipBgService),
            Message = $"Security camera {request.Source.DisplayName} detected "
                + $"{request.EventType}; video clip unavailable",
            Environment = env.GetAcronym(),
            TimestampUtc = timeProvider.GetUtcNow().UtcDateTime,
        }, cancellationToken);
    }
}
