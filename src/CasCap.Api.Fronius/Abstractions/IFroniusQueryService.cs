namespace CasCap.Abstractions;

/// <summary>
/// Defines the public query operations exposed by the Fronius solar inverter service.
/// </summary>
public interface IFroniusQueryService
{
    /// <summary>
    /// Fronius Symo solar inverter raw power flow inverter object retrieval.
    /// </summary>
    Task<PowerFlowRealtimeData?> GetPowerFlowRealtimeData();

    /// <summary>
    /// Retrieves inverter real-time data including AC/DC voltage, current, power and energy.
    /// </summary>
    /// <param name="dataCollection">The data collection to retrieve (CommonInverterData, 3PInverterData, or CumulationInverterData).</param>
    Task<CommonInverterData?> GetInverterRealtimeData(string dataCollection = "CommonInverterData");

    /// <summary>
    /// Retrieves inverter device information including custom name, serial number and state.
    /// </summary>
    Task<Dictionary<string, InverterInfoEntry>?> GetInverterInfo();

    /// <summary>
    /// Retrieves active device information for all connected inverters, meters, storage and Ohmpilot devices.
    /// </summary>
    Task<ActiveDeviceInfoData?> GetActiveDeviceInfo();

    /// <summary>
    /// Retrieves meter real-time data including power, voltage, current and energy measurements.
    /// </summary>
    /// <param name="scope">The scope of the query ('Device' or 'System'). Default is 'System'.</param>
    /// <param name="deviceId">Optional device ID when scope is 'Device'.</param>
    Task<Dictionary<string, MeterRealtimeData>?> GetMeterRealtimeData(string scope = "System", int? deviceId = null);

    /// <summary>
    /// Retrieves battery storage real-time data including state of charge, capacity and temperature.
    /// </summary>
    /// <param name="scope">The scope of the query ('Device' or 'System'). Default is 'System'.</param>
    /// <param name="deviceId">Optional device ID when scope is 'Device'.</param>
    Task<Dictionary<string, StorageRealtimeData>?> GetStorageRealtimeData(string scope = "System", int? deviceId = null);

    /// <summary>
    /// Fronius Symo inverter snapshot of current electrical consumption, grid consumption, solar production and battery state of charge value.
    /// </summary>
    Task<InverterSnapshot> GetInverterSnapshot();

    /// <summary>
    /// Retrieves inverter line item readings for the current day.
    /// </summary>
    /// <param name="limit">Maximum number of records to return. Default 100, maximum 1000.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<FroniusEvent> GetInverterReadings(int limit = 100, CancellationToken cancellationToken = default);
}
