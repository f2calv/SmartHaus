namespace CasCap.Models.Dtos;

/// <summary>Generic Wiz protocol response envelope.</summary>
/// <typeparam name="T">Type of the result payload.</typeparam>
public record WizResponse<T>
{
    /// <summary>The method that was invoked.</summary>
    [JsonPropertyName("method")]
    public string? Method { get; init; }

    /// <summary>Environment identifier.</summary>
    [JsonPropertyName("env")]
    public string? Env { get; init; }

    /// <summary>Result payload.</summary>
    [JsonPropertyName("result")]
    public T? Result { get; init; }

    /// <summary>Error payload if the command failed.</summary>
    [JsonPropertyName("error")]
    public WizError? Error { get; init; }
}
