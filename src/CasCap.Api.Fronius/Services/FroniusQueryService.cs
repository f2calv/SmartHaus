namespace CasCap.Services;

/// <summary>
/// Provides access to Fronius solar data by querying the primary sink or the inverter directly.
/// </summary>
/// <remarks>
/// Key functions are also made accessible via <see cref="Controllers.FroniusController"/>.
/// Queries are delegated to the keyed <see cref="SinkServiceCollectionExtensions.PrimarySinkKey"/> sink.
/// </remarks>
public class FroniusQueryService(
    ILogger<FroniusQueryService> logger,
    FroniusClientService clientSvc,
    [FromKeyedServices(SinkServiceCollectionExtensions.PrimarySinkKey)] IEventSink<FroniusEvent> primarySink,
    IFroniusQuery? froniusQuery = null) : IFroniusQueryService
{
    /// <summary>
    /// Fronius Symo solar inverter raw power flow inverter object retrieval.
    /// </summary>
    public async Task<PowerFlowRealtimeData?> GetPowerFlowRealtimeData()
    {
        logger.LogDebug("{ClassName} retrieving power flow realtime data", nameof(FroniusQueryService));
        var x = await clientSvc.GetPowerFlowRealtimeData();
        return x?.Body?.Data;
    }

    /// <summary>
    /// Retrieves inverter real-time data including AC/DC voltage, current, power and energy.
    /// </summary>
    /// <param name="dataCollection">The data collection to retrieve (CommonInverterData, 3PInverterData, or CumulationInverterData).</param>
    public async Task<CommonInverterData?> GetInverterRealtimeData(
        string dataCollection = "CommonInverterData")
    {
        var x = await clientSvc.GetInverterRealtimeData(dataCollection);
        return x?.Body?.Data;
    }

    /// <summary>
    /// Retrieves inverter device information including custom name, serial number and state.
    /// </summary>
    public async Task<Dictionary<string, InverterInfoEntry>?> GetInverterInfo()
    {
        var x = await clientSvc.GetInverterInfo();
        return x?.Body?.Data;
    }

    /// <summary>
    /// Retrieves active device information for all connected inverters, meters, storage and Ohmpilot devices.
    /// </summary>
    public async Task<ActiveDeviceInfoData?> GetActiveDeviceInfo()
    {
        var x = await clientSvc.GetActiveDeviceInfo();
        return x?.Body?.Data;
    }

    /// <summary>
    /// Retrieves meter real-time data including power, voltage, current and energy measurements.
    /// </summary>
    /// <param name="scope">The scope of the query ('Device' or 'System'). Default is 'System'.</param>
    /// <param name="deviceId">Optional device ID when scope is 'Device'.</param>
    public async Task<Dictionary<string, MeterRealtimeData>?> GetMeterRealtimeData(
        string scope = "System",
        int? deviceId = null)
    {
        var x = await clientSvc.GetMeterRealtimeData(scope, deviceId);
        return x?.Body?.Data;
    }

    /// <summary>
    /// Retrieves battery storage real-time data including state of charge, capacity and temperature.
    /// </summary>
    /// <param name="scope">The scope of the query ('Device' or 'System'). Default is 'System'.</param>
    /// <param name="deviceId">Optional device ID when scope is 'Device'.</param>
    public async Task<Dictionary<string, StorageRealtimeData>?> GetStorageRealtimeData(
        string scope = "System",
        int? deviceId = null)
    {
        var x = await clientSvc.GetStorageRealtimeData(scope, deviceId);
        return x?.Body?.Data;
    }

    /// <summary>
    /// Fronius Symo inverter snapshot of current electrical consumption, grid consumption, solar production and battery state of charge value.
    /// </summary>
    public async Task<InverterSnapshot> GetInverterSnapshot()
    {
        if (froniusQuery is null)
            return new();
        return await froniusQuery.GetSnapshot();
    }

    /// <summary>
    /// Retrieves inverter line item readings for the current day.
    /// </summary>
    /// <param name="limit">Maximum number of records to return. Default 100, maximum 1000.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public IAsyncEnumerable<FroniusEvent> GetInverterReadings(
        int limit = 100,
        CancellationToken cancellationToken = default)
        => primarySink.GetEvents(limit: limit, cancellationToken: cancellationToken);
}
