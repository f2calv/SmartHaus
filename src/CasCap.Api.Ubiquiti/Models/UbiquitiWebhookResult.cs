namespace CasCap.Models;

/// <summary>Describes webhook acceptance or a payload validation failure.</summary>
public sealed record UbiquitiWebhookResult
{
    /// <summary>Gets the validation error, or null after successful dispatch.</summary>
    /// <example>Invalid UniFi Protect webhook payload.</example>
    public string? Error { get; init; }
}
