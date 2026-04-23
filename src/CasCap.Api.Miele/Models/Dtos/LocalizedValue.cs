namespace CasCap.Models.Dtos;

/// <summary>
/// A localized key/value pair returned by the Miele API.
/// </summary>
public record LocalizedValue
{
    /// <summary>
    /// The raw integer value.
    /// </summary>
    [Description("The raw integer value.")]
    public int? value_raw { get; init; }

    /// <summary>
    /// The localized string representation of the value.
    /// </summary>
    [Description("The localized string representation of the value.")]
    public string? value_localized { get; init; }

    /// <summary>
    /// The localized name of the key.
    /// </summary>
    [Description("The localized name of the key.")]
    public string? key_localized { get; init; }
}
