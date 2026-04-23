namespace CasCap.Models.Dtos;

/// <summary>
/// BHA response containing session authentication details.
/// </summary>
public record BHASession
{
    /// <summary>
    /// The API return code.
    /// </summary>
    [Description("The API return code.")]
    [JsonPropertyName("RETURNCODE")]
    public required string ReturnCode { get; init; }

    /// <summary>
    /// The session identifier.
    /// </summary>
    [Description("The session identifier.")]
    [JsonPropertyName("SESSIONID")]
    public required string SessionId { get; init; }

    /// <summary>
    /// The encryption type used for this session.
    /// </summary>
    [Description("The encryption type used for this session.")]
    [JsonPropertyName("ENCRYPTION_TYPE")]
    public int EncryptionType { get; init; }

    /// <summary>
    /// The encryption key for this session.
    /// </summary>
    [Description("The encryption key for this session.")]
    [JsonPropertyName("ENCRYPTION_KEY")]
    public required string EncryptionKey { get; init; }
}
