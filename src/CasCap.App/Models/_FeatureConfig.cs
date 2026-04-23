namespace CasCap.Models;

/// <summary>Strongly-typed configuration for the comma-separated feature-flag string.</summary>
/// <remarks>
/// Binds from <c>CasCap:FeatureConfig:EnabledFeatures</c> (environment variable or
/// <c>appsettings.json</c>). The raw value is a comma-separated list of feature names
/// (e.g. <c>"Knx,Fronius,DoorBird"</c>) which <see cref="GetEnabledFeatures"/> splits
/// into a case-insensitive <see cref="HashSet{T}"/>.
/// </remarks>
public record FeatureConfig : IAppConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(FeatureConfig)}";

    /// <summary>Comma-separated list of enabled feature names.</summary>
    [Required]
    public required string EnabledFeatures { get; init; }

    /// <summary>Parses <see cref="EnabledFeatures"/> into a case-insensitive set of feature names.</summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="EnabledFeatures"/> contains a value not present in
    /// <see cref="FeatureNames.ValidNames"/>.
    /// </exception>
    public HashSet<string> GetEnabledFeatures()
    {
        var features = EnabledFeatures
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unknown = features.Where(f => !FeatureNames.ValidNames.Contains(f)).ToList();
        if (unknown.Count > 0)
            throw new InvalidOperationException(
                $"Unrecognised feature name(s): {string.Join(", ", unknown)}. " +
                $"Valid names: {string.Join(", ", FeatureNames.ValidNames)}.");

        return features;
    }
}
