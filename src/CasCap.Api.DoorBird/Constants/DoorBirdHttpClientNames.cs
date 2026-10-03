namespace CasCap.Constants;

/// <summary>Named HTTP clients used by the DoorBird LAN API integration.</summary>
public static class DoorBirdHttpClientNames
{
    /// <summary>Receive-only streaming client for the DoorBird microphone endpoint.</summary>
    public const string Audio = $"{nameof(DoorBirdClientService)}.Audio";
}
