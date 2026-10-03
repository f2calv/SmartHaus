namespace CasCap.Services;

/// <summary>Applies private source mapping, cooldowns, and bounded admission for camera clip events.</summary>
/// <param name="config">Camera clip options.</param>
/// <param name="timeProvider">Time source used for cooldown decisions.</param>
public sealed class CameraClipQueue(
    IOptions<CameraClipConfig> config,
    TimeProvider timeProvider)
{
    private readonly Channel<CameraClipRequest> _channel = Channel.CreateBounded<CameraClipRequest>(
        new BoundedChannelOptions(config.Value.QueueCapacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait,
        });
    private readonly Dictionary<string, DateTime> _lastAcceptedUtc =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _admissionLock = new();

    internal ChannelReader<CameraClipRequest> Reader => _channel.Reader;

    internal CameraClipAdmission TryEnqueue(UbiquitiEvent @event)
    {
        if (!config.Value.Enabled
            || @event.SourceCameraId is not { Length: > 0 } cameraId
            || !config.Value.Sources.TryGetValue(cameraId, out var source))
        {
            return CameraClipAdmission.NotConfigured;
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        lock (_admissionLock)
        {
            if (_lastAcceptedUtc.TryGetValue(source.Path, out var lastAcceptedUtc)
                && nowUtc - lastAcceptedUtc < TimeSpan.FromSeconds(source.CooldownSeconds))
            {
                return CameraClipAdmission.Suppressed;
            }

            if (!_channel.Writer.TryWrite(new CameraClipRequest(@event, source)))
                return CameraClipAdmission.QueueFull;

            _lastAcceptedUtc[source.Path] = nowUtc;
            return CameraClipAdmission.Enqueued;
        }
    }

    internal bool IsConfigured(string? cameraId)
        => config.Value.Enabled
            && cameraId is { Length: > 0 }
            && config.Value.Sources.ContainsKey(cameraId);
}
