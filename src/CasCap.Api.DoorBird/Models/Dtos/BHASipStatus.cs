namespace CasCap.Models.Dtos;

/// <summary>
/// BHA response containing SIP status information.
/// </summary>
public record BHASipStatus
{
    /// <summary>
    /// The API return code.
    /// </summary>
    [Description("The API return code.")]
    [JsonPropertyName("RETURNCODE")]
    public required string ReturnCode { get; init; }

    /// <summary>
    /// Whether SIP is enabled on the device.
    /// </summary>
    [Description("Whether SIP is enabled on the device.")]
    [JsonPropertyName("ENABLE")]
    public required string Enable { get; init; }

    /// <summary>
    /// Whether incoming SIP calls are allowed.
    /// </summary>
    [Description("Whether incoming SIP calls are allowed.")]
    [JsonPropertyName("INCOMING_CALL_ENABLE")]
    public required string IncomingCallEnable { get; init; }

    /// <summary>
    /// Whether the ANC (Acoustic Noise Cancellation) is enabled.
    /// </summary>
    [Description("Whether the ANC (Acoustic Noise Cancellation) is enabled.")]
    [JsonPropertyName("ANC_ENABLE")]
    public required string AncEnable { get; init; }

    /// <summary>
    /// Whether relay activation via DTMF is enabled.
    /// </summary>
    [Description("Whether relay activation via DTMF is enabled.")]
    [JsonPropertyName("RELAY_ENABLE_VIA_DTMF")]
    public required string RelayEnableViaDtmf { get; init; }

    /// <summary>
    /// The call timeout in seconds when no one answers.
    /// </summary>
    [Description("The call timeout in seconds when no one answers.")]
    [JsonPropertyName("INCOMING_CALL_NO_ANSWER_TIMEOUT")]
    public required string IncomingCallNoAnswerTimeout { get; init; }

    /// <summary>
    /// Whether to prioritize app calls.
    /// </summary>
    [Description("Whether to prioritize app calls.")]
    [JsonPropertyName("PRIORITIZE_APP")]
    public required string PrioritizeApp { get; set; }

    /// <summary>
    /// The SIP registration URL.
    /// </summary>
    [Description("The SIP registration URL.")]
    [JsonPropertyName("REGISTER_URL")]
    public required string RegisterUrl { get; set; }

    /// <summary>
    /// The SIP registration username.
    /// </summary>
    [Description("The SIP registration username.")]
    [JsonPropertyName("REGISTER_USER")]
    public required string RegisterUser { get; set; }

    /// <summary>
    /// The SIP registration authorization ID.
    /// </summary>
    [Description("The SIP registration authorization ID.")]
    [JsonPropertyName("REGISTER_AUTH_ID")]
    public required string RegisterAuthId { get; set; }

    /// <summary>
    /// The SIP registration display name.
    /// </summary>
    [Description("The SIP registration display name.")]
    [JsonPropertyName("REGISTER_DISPLAY_NAME")]
    public required string RegisterDisplayName { get; set; }

    /// <summary>
    /// The SIP registration password.
    /// </summary>
    [Description("The SIP registration password.")]
    [JsonPropertyName("REGISTER_PASSWORD")]
    public required string RegisterPassword { get; set; }

    /// <summary>
    /// The URL to call when motion sensor is triggered.
    /// </summary>
    [Description("The URL to call when motion sensor is triggered.")]
    [JsonPropertyName("AUTOCALL_MOTIONSENSOR_URL")]
    public required string AutocallMotionSensorUrl { get; set; }

    /// <summary>
    /// The URL to call when doorbell is pressed.
    /// </summary>
    [Description("The URL to call when doorbell is pressed.")]
    [JsonPropertyName("AUTOCALL_DOORBELL_URL")]
    public required string AutocallDoorbellUrl { get; set; }

    /// <summary>
    /// Speaker volume level.
    /// </summary>
    [Description("Speaker volume level.")]
    [JsonPropertyName("SPK_VOLUME")]
    public required string SpkVolume { get; set; }

    /// <summary>
    /// Microphone volume level.
    /// </summary>
    [Description("Microphone volume level.")]
    [JsonPropertyName("MIC_VOLUME")]
    public required string MicVolume { get; set; }

    /// <summary>
    /// DTMF configuration.
    /// </summary>
    [Description("DTMF configuration.")]
    [JsonPropertyName("DTMF")]
    public required string Dtmf { get; set; }

    /// <summary>
    /// Relay 1 configuration.
    /// </summary>
    [Description("Relay 1 configuration.")]
    [JsonPropertyName("RELAIS1")]
    public required string Relais1 { get; set; }

    /// <summary>
    /// Relay 2 configuration.
    /// </summary>
    [Description("Relay 2 configuration.")]
    [JsonPropertyName("RELAIS2")]
    public required string Relais2 { get; set; }

    /// <summary>
    /// The passcode for light activation.
    /// </summary>
    [Description("The passcode for light activation.")]
    [JsonPropertyName("LIGHT_PASSCODE")]
    public required string LightPasscode { get; set; }

    /// <summary>
    /// Whether to hang up when button is pressed.
    /// </summary>
    [Description("Whether to hang up when button is pressed.")]
    [JsonPropertyName("HANGUP_ON_BUTTON_PRESS")]
    public required string HangupOnButtonPress { get; set; }

    /// <summary>
    /// The incoming call user identifier.
    /// </summary>
    [Description("The incoming call user identifier.")]
    [JsonPropertyName("INCOMING_CALL_USER")]
    public required string IncomingCallUser { get; set; }

    /// <summary>
    /// ANC (Acoustic Noise Cancellation) configuration.
    /// </summary>
    [Description("ANC (Acoustic Noise Cancellation) configuration.")]
    [JsonPropertyName("ANC")]
    public required string Anc { get; set; }

    /// <summary>
    /// The last error code.
    /// </summary>
    [Description("The last error code.")]
    [JsonPropertyName("LASTERRORCODE")]
    public required string LastErrorCode { get; set; }

    /// <summary>
    /// The last error text.
    /// </summary>
    [Description("The last error text.")]
    [JsonPropertyName("LASTERRORTEXT")]
    public required string LastErrorText { get; set; }

    /// <summary>
    /// Ring time limit in seconds.
    /// </summary>
    [Description("Ring time limit in seconds.")]
    [JsonPropertyName("RING_TIME_LIMIT")]
    public required string RingTimeLimit { get; set; }

    /// <summary>
    /// Call time limit in seconds.
    /// </summary>
    [Description("Call time limit in seconds.")]
    [JsonPropertyName("CALL_TIME_LIMIT")]
    public required string CallTimeLimit { get; set; }

    /// <summary>
    /// STUN server configuration.
    /// </summary>
    [Description("STUN server configuration.")]
    [JsonPropertyName("STUN_SRV")]
    public required string StunSrv { get; set; }
}
