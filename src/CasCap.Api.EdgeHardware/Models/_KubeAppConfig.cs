using System.Net;

namespace CasCap.Models;

/// <summary>
/// Lightweight internal projection of the <c>AppConfig</c> section that exposes only
/// <see cref="IKubeAppConfig"/> properties. Used by <see cref="CasCap.Services.EdgeHardwareSinkMetricsService"/>
/// to tag metrics with the Kubernetes node name without depending on the full <c>AppConfig</c> type.
/// </summary>
internal record KubeAppConfig : IAppConfig, IKubeAppConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => "AppConfig";

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
}
