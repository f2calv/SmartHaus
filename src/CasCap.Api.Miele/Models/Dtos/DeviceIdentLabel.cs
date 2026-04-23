namespace CasCap.Models.Dtos;

/// <summary>
/// Hardware identification label for a Miele device.
/// </summary>
public record DeviceIdentLabel
{
    /// <summary>
    /// The serial number of the device.
    /// </summary>
    [Description("The serial number of the device.")]
    public string? fabNumber { get; init; }

    /// <summary>
    /// The fabrication index.
    /// </summary>
    [Description("The fabrication index.")]
    public string? fabIndex { get; init; }

    /// <summary>
    /// The technical type of the device.
    /// </summary>
    [Description("The technical type of the device.")]
    public string? techType { get; init; }

    /// <summary>
    /// The material number of the device.
    /// </summary>
    [Description("The material number of the device.")]
    public string? matNumber { get; init; }

    /// <summary>
    /// List of all software IDs.
    /// </summary>
    [Description("List of all software IDs.")]
    public string[]? swids { get; init; }
}

/// <summary>
/// Communication module identification label.
/// </summary>
public record XkmIdentLabel
{
    /// <summary>
    /// The technical type of the communication module.
    /// </summary>
    [Description("The technical type of the communication module.")]
    public string? techType { get; init; }

    /// <summary>
    /// The release version of the communication module.
    /// </summary>
    [Description("The release version of the communication module.")]
    public string? releaseVersion { get; init; }
}
