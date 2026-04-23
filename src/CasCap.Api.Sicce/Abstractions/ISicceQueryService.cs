namespace CasCap.Abstractions;

/// <summary>
/// Defines the public query operations exposed by the Sicce water pump service.
/// </summary>
public interface ISicceQueryService
{
    /// <summary>
    /// Retrieves the latest device information directly from the Sicce cloud API.
    /// </summary>
    Task<DeviceInfo?> GetDeviceInfo();

    /// <summary>
    /// Retrieves the latest Sicce device snapshot from the primary sink.
    /// </summary>
    Task<SicceSnapshot> GetSnapshot();

    /// <summary>
    /// Retrieves recent Sicce device readings from the primary sink.
    /// </summary>
    /// <param name="limit">Maximum number of records to return. Default 100, maximum 1000.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<SicceEvent> GetReadings(int limit = 100, CancellationToken cancellationToken = default);
}
