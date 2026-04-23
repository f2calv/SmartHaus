using Knx.Falcon.Sdk;
using System.Collections.Concurrent;

namespace CasCap.Services;

/// <summary>
/// Internal collections shared between the KNX bus monitor, sender &amp; processor services.
/// </summary>
public static class KnxStatics
{
    /// <summary>
    /// Thread-safe map of active KNX bus connections keyed by area/line.
    /// </summary>
    public static ConcurrentDictionary<KnxAreaLine, KnxBus?> BusConnections { get; } = new();
}
