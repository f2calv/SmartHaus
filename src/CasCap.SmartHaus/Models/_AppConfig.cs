using Azure.Core;

namespace CasCap.Models;

/// <summary>
/// General application configuration.
/// </summary>
public record AppConfig : IAppConfig, IAzureAuthConfig, IKubeAppConfig, IMetricsConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => nameof(AppConfig);

    #region Azure Authentication
    /// <inheritdoc/>
    public bool IsKeyVaultEnabled =>
        !string.Equals(KeyVaultName, IAzureAuthConfig.SkipKeyVaultSentinel, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    [Required, MinLength(3)]
    public required string KeyVaultName { get; init; }

    /// <inheritdoc/>
    public Uri KeyVaultUri => new Uri($"https://{KeyVaultName}.vault.azure.net/");

    /// <inheritdoc/>
    public Guid? AzureEntraPodManagedIdentityClientId { get; init; }

    /// <inheritdoc/>
    public Guid? AzureEntraTenantId { get; init; }

    /// <inheritdoc/>
    public Guid? AzureEntraApplicationId { get; init; }

    /// <inheritdoc/>
    public string? AzureEntraCertThumbprint { get; init; }

    /// <inheritdoc/>
    public string? AzureEntraPfxPath { get; init; }

    /// <inheritdoc/>
    public string? AzureEntraPfxPassword { get; init; }

    private TokenCredential? tokenCredential;

    /// <inheritdoc/>
    public TokenCredential? TokenCredential
    {
        get
        {
            tokenCredential ??= TokenCredentialExtensions.CreateTokenCredential(this);
            return tokenCredential;
        }
    }
    #endregion

    /// <summary>
    /// Unique installation/tenant name used as the default partition key
    /// for Azure Table Storage snapshot and CEMI entities.
    /// </summary>
    [Required, MinLength(1)]
    public required string HausName { get; init; } = "SmartHaus";

    /// <summary>
    /// Prefix applied to all OpenTelemetry metric names (e.g. <c>"haus"</c> produces <c>haus.knx.hvac.temp</c>).
    /// Also used as the OTel <see cref="System.Diagnostics.Metrics.Meter"/> name.
    /// </summary>
    /// <remarks>Defaults to <c>"haus"</c>.</remarks>
    [Required]
    public string MetricNamePrefix { get; init; } = "haus";

    /// <inheritdoc/>
    /// <remarks>Defaults to <c>"CasCap.App"</c>.</remarks>
    [Required]
    public string OtelServiceName { get; init; } = "CasCap.App";

    #region Kubernetes specific
    /// <inheritdoc/>
    public string? NodeName { get; init; }

    /// <inheritdoc/>
    public string? PodName { get; init; }

    /// <inheritdoc/>
    public string? Namespace { get; init; }

    /// <inheritdoc/>
    public IPAddress? PodIp { get; init; }

    /// <inheritdoc/>
    public string? ServiceAccountName { get; init; }
    #endregion

    #region Swagger
    /// <summary>The URL path prefix for the Swagger UI.</summary>
    /// <remarks>Defaults to <c>"swagger"</c>.</remarks>
    public string SwaggerUriRoutePrefix { get; init; } = "swagger";

    /// <summary>
    /// Swagger spec endpoints to display in the Swagger UI dropdown. Key = display name, Value = spec URL.
    /// </summary>
    /// <remarks>
    /// Locally a single entry pointing to the default spec is sufficient.
    /// In production each pod's spec is listed, allowing a single Swagger UI to aggregate multiple APIs.
    /// </remarks>
    public Dictionary<string, string> SwaggerEndpoints { get; init; } = new();
    #endregion
}
