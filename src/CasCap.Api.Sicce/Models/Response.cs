namespace CasCap.Models;

/// <summary>Base response from Sicce API.</summary>
public class Response
{
    /// <summary>Operation result status.</summary>
    [JsonPropertyName("result")]
    public bool Result { get; set; }

    /// <summary>
    /// Message error if <see cref="Result"/> is false.
    /// </summary>
    [JsonPropertyName("error")]
    public string Error { get; set; } = default!;
}
