namespace CasCap.Models;

/// <summary>Connection strings configuration.</summary>
public record ConnectionStrings : IAppConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => nameof(ConnectionStrings);

    /// <summary>Web API endpoint URI.</summary>
    [Required]
    public required Uri WebApi { get; init; }

    /// <summary>OTLP exporter endpoint URI.</summary>
    public Uri? OtlpExporter { get; init; }
}
