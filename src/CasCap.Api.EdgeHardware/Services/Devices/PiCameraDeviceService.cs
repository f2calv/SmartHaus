using MMALSharp;
using MMALSharp.Common;
using MMALSharp.Common.Utility;
using MMALSharp.Handlers;

namespace CasCap.Services;

/// <summary>Raspberry Pi camera device implementation using MMALSharp.</summary>
public class PiCameraDeviceService : ICameraDevice, IDisposable
{
    private readonly ILogger _logger;
    private readonly EdgeHardwareConfig _edgeHardwareConfig;

    private readonly MMALCamera _camera;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="PiCameraDeviceService"/> class.</summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="edgeHardwareConfig">Edge hardware configuration.</param>
    public PiCameraDeviceService(ILogger<PiCameraDeviceService> logger, IOptions<EdgeHardwareConfig> edgeHardwareConfig)
    {
        _logger = logger;
        _edgeHardwareConfig = edgeHardwareConfig.Value;
        _camera = MMALCamera.Instance;
        MMALCameraConfig.StillResolution = new Resolution(1280, 720); // Set to 640 x 480. Default is 2560 x 1920.
        //MMALCameraConfig.VideoFramerate = new MMALSharp.Native.MMAL_RATIONAL_T(20, 1); // Set to 20fps. Default is 30fps.
        //MMALCameraConfig.ShutterSpeed = 2000000; // Set to 2s exposure time. Default is 0 (auto).
        //MMALCameraConfig.ISO = 400; // Set ISO to 400. Default is 0 (auto).
        MMALCameraConfig.Rotation = 180;
        _logger.LogInformation("{ClassName} started", nameof(PiCameraDeviceService));
    }

    /// <inheritdoc/>
    public async Task<(string, DateTime)> TakePicture(DateTime? dtStamp = null, string format = "yyyy-MM-dd-HH-mm-ss-fff")
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!dtStamp.HasValue) dtStamp = DateTime.UtcNow;
        var fullPath = Path.Combine(_edgeHardwareConfig.LocalPath!, $"{dtStamp.Value.ToString(format)}.jpg");
        using (var handler = new ImageStreamCaptureHandler(fullPath))
        {
            await _camera.TakePicture(handler, MMALEncoding.JPEG, MMALEncoding.I420);
        }
        return (fullPath, dtStamp.Value);
    }

    /// <inheritdoc/>
    public async Task<(string, DateTime)> TakeVideo(TimeSpan duration, DateTime? timestampUtc = null, string format = "yyyy-MM-dd-HH-mm-ss-fff")
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!timestampUtc.HasValue) timestampUtc = DateTime.UtcNow;
        var fullPath = Path.Combine(_edgeHardwareConfig.LocalPath!, $"{timestampUtc.Value.ToString(format)}.avi");
        using (var handler = new VideoStreamCaptureHandler(fullPath))
        {
            var cts = new CancellationTokenSource(duration);
            await _camera.TakeVideo(handler, cts.Token);
            cts.Dispose();
        }
        return (fullPath, timestampUtc.Value);
    }

    /// <inheritdoc/>
    public async Task<(byte[], DateTime)> StoreToMemory()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        byte[]? bytes = null;
        var dtStamp = DateTime.UtcNow;
        using (var handler = new InMemoryCaptureHandler())
        {
            await _camera.TakeRawPicture(handler);
            // Access raw unencoded output.
            bytes = handler.WorkingData.ToArray();
        }
        return (bytes, dtStamp);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases resources used by the camera.</summary>
    /// <remarks>Calls <see cref="MMALCamera.Cleanup"/> to dispose all unmanaged resources and unload the Broadcom library.</remarks>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
            _camera?.Cleanup();

        _disposed = true;
    }
}
