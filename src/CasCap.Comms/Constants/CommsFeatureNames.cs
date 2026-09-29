namespace CasCap.Constants;

/// <summary>Feature names owned by the shared communications projects.</summary>
public static class CommsFeatureNames
{
    /// <summary>The feature that runs <see cref="CasCap.Services.CommunicationsBgService"/>.</summary>
    /// <remarks>Matches the host application's own <c>Comms</c> feature name.</remarks>
    public const string Comms = nameof(Comms);
}
