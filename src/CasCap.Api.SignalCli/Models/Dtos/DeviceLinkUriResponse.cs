namespace CasCap.Models.Dtos;

/// <summary>
/// Represents the response from the <c>GET /v1/qrcodelink/raw</c> endpoint.
/// </summary>
public record DeviceLinkUriResponse
{
    /// <summary>
    /// The device-link URI used to link a new device.
    /// </summary>
    [JsonPropertyName("device_link_uri")]
    public string? DeviceLinkUri { get; init; }
}
