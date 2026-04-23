namespace CasCap.Models.Dtos;

/// <summary>Error payload returned by the Wiz bulb protocol.</summary>
public record WizError
{
    /// <summary>Numeric error code.</summary>
    [JsonPropertyName("code")]
    public int Code { get; init; }

    /// <summary>Human-readable error message.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}
