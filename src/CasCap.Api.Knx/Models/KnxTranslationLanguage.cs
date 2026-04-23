namespace CasCap.Models;

/// <summary>
/// Translation dictionaries for a single language, mapping English enum values
/// to their localised equivalents.
/// </summary>
public record KnxTranslationLanguage
{
    /// <summary>
    /// Floor abbreviation translations, e.g. <c>DG</c> → <c>Dachgeschoss</c>.
    /// </summary>
    public Dictionary<string, string> Floors { get; init; } = [];

    /// <summary>
    /// Room name translations, e.g. <c>Kitchen</c> → <c>Küche</c>.
    /// </summary>
    public Dictionary<string, string> Rooms { get; init; } = [];

    /// <summary>
    /// Compass orientation translations, e.g. <c>North</c> → <c>Nord</c>.
    /// </summary>
    public Dictionary<string, string> Orientations { get; init; } = [];

    /// <summary>
    /// Group address category translations, e.g. <c>LI</c> → <c>Beleuchtung</c>.
    /// </summary>
    public Dictionary<string, string> Categories { get; init; } = [];
}
