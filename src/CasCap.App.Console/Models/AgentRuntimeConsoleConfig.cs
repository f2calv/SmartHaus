namespace CasCap.Models;

/// <summary>Configures the interactive Agent Runtime console client.</summary>
public sealed record AgentRuntimeConsoleConfig : IAppConfig, IValidatableObject
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(AgentRuntimeConsoleConfig)}";

    /// <summary>Gets the tenant agent names available in the console selector.</summary>
    [Required, MinLength(1)]
    public string[] AgentNames { get; init; } = ["CommsAgent"];

    /// <summary>Gets the caller-owned session identifier shared across console turns.</summary>
    [Required, MinLength(1), MaxLength(200)]
    public string SessionId { get; init; } = "smarthaus-console";

    /// <summary>Gets whether operator-only diagnostic details are requested from the runtime.</summary>
    public bool DiagnosticDetailsEnabled { get; init; }

    /// <inheritdoc/>
    public IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AgentNames.Any(string.IsNullOrWhiteSpace))
            yield return new System.ComponentModel.DataAnnotations.ValidationResult(
                $"{nameof(AgentNames)} cannot contain empty names.",
                [nameof(AgentNames)]);

        if (AgentNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() != AgentNames.Length)
            yield return new System.ComponentModel.DataAnnotations.ValidationResult(
                $"{nameof(AgentNames)} cannot contain duplicate names.",
                [nameof(AgentNames)]);
    }
}