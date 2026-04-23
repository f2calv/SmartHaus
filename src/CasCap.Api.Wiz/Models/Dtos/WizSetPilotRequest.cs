namespace CasCap.Models.Dtos;

/// <summary>Request payload for the Wiz <c>setPilot</c> command.</summary>
public record WizSetPilotRequest
{
    /// <summary>Whether to turn the bulb on or off.</summary>
    [Description("Turn the bulb on (true) or off (false).")]
    [JsonPropertyName("state")]
    public bool? State { get; init; }

    /// <summary>Brightness percentage, 10–100.</summary>
    [Description("Brightness percentage, 10–100.")]
    [JsonPropertyName("dimming")]
    public int? Dimming { get; init; }

    /// <summary>Colour temperature in Kelvin, typically 2200–6500.</summary>
    [Description("Colour temperature in Kelvin, typically 2200–6500.")]
    [JsonPropertyName("temp")]
    public int? Temp { get; init; }

    /// <summary>Red colour channel, 0–255.</summary>
    [Description("Red colour channel, 0–255.")]
    [JsonPropertyName("r")]
    public int? R { get; init; }

    /// <summary>Green colour channel, 0–255.</summary>
    [Description("Green colour channel, 0–255.")]
    [JsonPropertyName("g")]
    public int? G { get; init; }

    /// <summary>Blue colour channel, 0–255.</summary>
    [Description("Blue colour channel, 0–255.")]
    [JsonPropertyName("b")]
    public int? B { get; init; }

    /// <summary>Cold white channel, 0–255.</summary>
    [Description("Cold white channel, 0–255.")]
    [JsonPropertyName("c")]
    public int? C { get; init; }

    /// <summary>Warm white channel, 0–255.</summary>
    [Description("Warm white channel, 0–255.")]
    [JsonPropertyName("w")]
    public int? W { get; init; }

    /// <summary>Scene identifier to activate.</summary>
    [Description("Scene ID, e.g. 1=Ocean, 2=Romance, 3=Sunset, 4=Party, 5=Fireplace.")]
    [JsonPropertyName("sceneId")]
    public int? SceneId { get; init; }

    /// <summary>Scene playback speed, 20–200.</summary>
    [Description("Scene playback speed, 20–200.")]
    [JsonPropertyName("speed")]
    public int? Speed { get; init; }
}
