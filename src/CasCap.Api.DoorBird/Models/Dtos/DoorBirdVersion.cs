namespace CasCap.Models.Dtos;

/// <summary>
/// DoorBird device version and hardware information.
/// </summary>
public record DoorBirdVersion
{
    /// <summary>
    /// The firmware version string.
    /// </summary>
    [JsonPropertyName("FIRMWARE")]
    public required string Firmware { get; init; }

    /// <summary>
    /// The firmware build number.
    /// </summary>
    [JsonPropertyName("BUILD_NUMBER")]
    public required string BuildNumber { get; init; }

    /// <summary>
    /// The primary MAC address of the device.
    /// </summary>
    [JsonPropertyName("PRIMARY_MAC_ADDR")]
    public string? PrimaryMacAddr { get; init; }

    /// <summary>
    /// The available relay identifiers on the device.
    /// </summary>
    [JsonPropertyName("RELAYS")]
    public required string[] Relays { get; init; }

    /// <summary>
    /// The device type identifier (e.g. "DoorBird D2101V").
    /// </summary>
    [JsonPropertyName("DEVICE-TYPE")]
    public required string DeviceType { get; init; }

    /// <summary>
    /// The Wi-Fi MAC address of the device.
    /// </summary>
    [JsonPropertyName("WIFI_MAC_ADDR")]
    public required string WifiMacAddr { get; init; }
}
