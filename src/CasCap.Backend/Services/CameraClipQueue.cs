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

        return TryEnqueue(CameraClipRequest.FromUbiquiti(@event, source));
    }

    internal CameraClipAdmission TryEnqueue(DoorBirdEvent @event)
    {
        if (!config.Value.Enabled
            || config.Value.DoorBirdSource is not { } source
            || @event.DoorBirdEventType is DoorBirdEventType.DoorRelay)
        {
            return CameraClipAdmission.NotConfigured;
        }

        return TryEnqueue(CameraClipRequest.FromDoorBird(@event, source));
    }

    private CameraClipAdmission TryEnqueue(CameraClipRequest request)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        lock (_admissionLock)
        {
            if (_lastAcceptedUtc.TryGetValue(request.Source.Path, out var lastAcceptedUtc)
                && nowUtc - lastAcceptedUtc < TimeSpan.FromSeconds(request.Source.CooldownSeconds))
            {
                return CameraClipAdmission.Suppressed;
            }

            if (!_channel.Writer.TryWrite(request))
                return CameraClipAdmission.QueueFull;

            _lastAcceptedUtc[request.Source.Path] = nowUtc;
            return CameraClipAdmission.Enqueued;
        }
    }

    internal bool IsUbiquitiConfigured(string? cameraId)
        => config.Value.Enabled
            && cameraId is { Length: > 0 }
            && config.Value.Sources.ContainsKey(cameraId);

    internal bool IsDoorBirdConfigured
        => config.Value.Enabled && config.Value.DoorBirdSource is not null;
}
