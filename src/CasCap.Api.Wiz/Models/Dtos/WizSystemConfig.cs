namespace CasCap.Models.Dtos;

/// <summary>Represents the system configuration of a Wiz bulb.</summary>
public record WizSystemConfig
{
    /// <summary>MAC address of the bulb.</summary>
    [Description("MAC address of the bulb.")]
    [JsonPropertyName("mac")]
    public string? Mac { get; init; }

    /// <summary>Module name identifying the hardware platform.</summary>
    [Description("Module name identifying the hardware platform.")]
    [JsonPropertyName("moduleName")]
    public string? ModuleName { get; init; }

    /// <summary>Firmware version string.</summary>
    [Description("Firmware version string.")]
    [JsonPropertyName("fwVersion")]
    public string? FwVersion { get; init; }

    /// <summary>Home identifier the bulb belongs to.</summary>
    [Description("Home identifier the bulb belongs to.")]
    [JsonPropertyName("homeId")]
    public int? HomeId { get; init; }

    /// <summary>Room identifier the bulb belongs to.</summary>
    [Description("Room identifier the bulb belongs to.")]
    [JsonPropertyName("roomId")]
    public int? RoomId { get; init; }

    /// <summary>Type identifier of the device.</summary>
    [Description("Type identifier of the device.")]
    [JsonPropertyName("typeId")]
    public int? TypeId { get; init; }

    /// <summary>Whether the device supports white colour channels.</summary>
    [Description("Whether the device supports white colour channels.")]
    [JsonPropertyName("whiteRange")]
    public int[]? WhiteRange { get; init; }

    /// <summary>Extended white colour range limits.</summary>
    [Description("Extended white colour range limits.")]
    [JsonPropertyName("extRange")]
    public int[]? ExtRange { get; init; }
}
