using System.Reflection;

namespace CasCap.Models;

/// <summary>
/// Well-known feature name constants for <see cref="CasCap.Common.Abstractions.IBgFeature.FeatureName"/>
/// and <see cref="FeatureConfig.EnabledFeatures"/>.
/// </summary>
/// <remarks>
/// Use these constants instead of string literals so that a feature rename is a
/// single point of change. Feature libraries declare their own <c>FeatureName</c>
/// strings; this class is for consumers (composition root, tests, XML docs).
/// </remarks>
public static class FeatureNames
{
    /// <summary>
    /// Case-insensitive set of all valid feature names derived from the <c>const string</c>
    /// fields on this class. Used by <see cref="FeatureConfig.GetEnabledFeatures"/> to
    /// reject unrecognised values at startup.
    /// </summary>
    public static readonly IReadOnlySet<string> ValidNames =
        typeof(FeatureNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Fronius solar inverter integration.</summary>
    public const string Fronius = nameof(Fronius);

    /// <summary>Buderus heating system integration.</summary>
    public const string Buderus = nameof(Buderus);

    /// <summary>KNX home automation integration.</summary>
    public const string Knx = nameof(Knx);

    /// <summary>DoorBird video doorbell integration.</summary>
    public const string DoorBird = nameof(DoorBird);

    /// <summary>Edge hardware monitoring — GPU telemetry, CPU temperature, and Raspberry Pi GPIO sensors.</summary>
    public const string EdgeHardware = nameof(EdgeHardware);

    /// <summary>Sicce aquarium pump integration.</summary>
    public const string Sicce = nameof(Sicce);

    /// <summary>Miele appliance integration.</summary>
    public const string Miele = nameof(Miele);

    /// <summary>Wiz smart lighting integration.</summary>
    public const string Wiz = nameof(Wiz);

    /// <summary>Dynamic DNS service.</summary>
    public const string DDns = nameof(DDns);

    /// <summary>Ubiquiti network device integration.</summary>
    public const string Ubiquiti = nameof(Ubiquiti);

    /// <summary>Shelly smart plug integration.</summary>
    public const string Shelly = nameof(Shelly);

    /// <summary>Consolidated SignalR hub server, providing a single real-time event endpoint for all features.</summary>
    public const string SignalRHub = nameof(SignalRHub);

    /// <summary>Single-instance communications and media analysis service — consumes key events and binary media from Redis Streams, routes media to domain agents, and relays notifications via Signal messenger.</summary>
    public const string Comms = nameof(Comms);

    /// <summary>Lightweight feature name used by integration tests to boot the application without activating any hardware features.</summary>
    public const string Test = nameof(Test);
}
