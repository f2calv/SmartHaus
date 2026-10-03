namespace CasCap.Models;

internal sealed record CameraClipRequest(
    UbiquitiEvent Event,
    CameraClipSourceConfig Source);
