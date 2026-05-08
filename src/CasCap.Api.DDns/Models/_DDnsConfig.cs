namespace CasCap.Models;

/// <summary>Dynamic DNS configuration.</summary>
public record DDnsConfig : IAppConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(DDnsConfig)}";

    /// <summary>Well-known feature name for the Dynamic DNS workload.</summary>
    public const string FeatureName = "DDns";

    /// <summary>The base address of the IP discovery service (e.g. <c>"https://api.ipify.org/"</c>).</summary>
    /// <remarks>Defaults to <c>"https://api.ipify.org/"</c>.</remarks>
    [Required, Url]
    public required string BaseAddress { get; init; } = "https://api.ipify.org/";

    /// <summary>Azure DNS resource group name.</summary>
    [Required]
    public required string DnsResourceGroupName { get; init; }

    /// <summary>Azure DNS zone name.</summary>
    [Required]
    public required string DnsZoneName { get; init; }

    /// <summary>DNS metadata key for tracking updates.</summary>
    [Required]
    public required string DnsMetaDataKey { get; init; }

    /// <summary>Delay in milliseconds between DNS refresh cycles.</summary>
    /// <remarks>Defaults to <c>30000</c> ms.</remarks>
    [Required, Range(1, int.MaxValue)]
    public required int RefreshDelayMs { get; init; } = 30_000;
}
