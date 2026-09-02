namespace CasCap.Tests;

/// <summary>
/// Integration tests for <see cref="FroniusClientService"/> against a real Fronius inverter.
/// </summary>
public sealed class FroniusClientServiceTests(ITestOutputHelper output) : TestBase(output)
{
    [Theory]
    [InlineData("solar_api/v1/GetPowerFlowRealtimeData.fcgi", typeof(ApiWrapper<PowerFlowRealtimeData>))]
    [InlineData("solar_api/v1/GetInverterRealtimeData.cgi?Datacollection=CommonInverterData", typeof(ApiWrapper<CommonInverterData>))]
    [InlineData("solar_api/v1/GetInverterRealtimeData.cgi?Datacollection=3PInverterData", typeof(ApiWrapper<CommonInverterData>))]
    [InlineData("solar_api/v1/GetInverterRealtimeData.cgi?Datacollection=CumulationInverterData", typeof(ApiWrapper<CommonInverterData>))]
    [InlineData("solar_api/v1/GetInverterInfo.cgi", typeof(ApiWrapper<Dictionary<string, InverterInfoEntry>>))]
    [InlineData("solar_api/v1/GetActiveDeviceInfo.cgi", typeof(ApiWrapper<ActiveDeviceInfoData>))]
    [InlineData("solar_api/v1/GetMeterRealtimeData.cgi?Scope=System", typeof(ApiWrapper<Dictionary<string, MeterRealtimeData>>))]
    [InlineData("solar_api/v1/GetStorageRealtimeData.cgi?Scope=System", typeof(ApiWrapper<Dictionary<string, StorageRealtimeData>>))]
    [Trait("Category", "Integration")]
    public async Task ApiPayloadMatchesDtoContract(string requestUri, Type dtoType)
    {
        var rawResponse = await svc.Client.GetStringAsync(requestUri, TestContext.Current.CancellationToken);
        var unmappedProperties = JsonContractAuditor.FindUnmappedProperties(rawResponse, dtoType);

        Assert.Empty(unmappedProperties);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetPowerFlowRealtimeData_ReturnsData()
    {
        var result = await svc.GetPowerFlowRealtimeData();
        Assert.NotNull(result);
        Assert.NotNull(result.Head);
        Assert.NotNull(result.Body?.Data);
        Assert.NotNull(result.Body.Data.Site);
        Assert.NotNull(result.Body.Data.Inverters);
        Assert.NotEmpty(result.Body.Data.Inverters);

        var site = result.Body.Data.Site;
        _output.WriteLine($"P_PV={site.P_PV}W, P_Grid={site.P_Grid}W, P_Load={site.P_Load}W, P_Akku={site.P_Akku}W");

        var inverter = result.Body.Data.Inverters.First();
        _output.WriteLine($"Inverter {inverter.Key}: SOC={inverter.Value.SOC}%, P={inverter.Value.P}W");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetInverterRealtimeData_CommonInverterData_ReturnsData()
    {
        var result = await svc.GetInverterRealtimeData("CommonInverterData");
        Assert.NotNull(result);
        Assert.NotNull(result.Head);
        Assert.NotNull(result.Body?.Data);
        Assert.NotNull(result.Body.Data.PAC);
        Assert.True(result.Body.Data.PAC.Value.HasValue || result.Body.Data.PAC.Values is { Count: > 0 });

        var pac = result.Body.Data.PAC.Value ?? result.Body.Data.PAC.Values?.Values.FirstOrDefault();
        var dayEnergy = result.Body.Data.DAY_ENERGY?.Value ?? result.Body.Data.DAY_ENERGY?.Values?.Values.FirstOrDefault();
        _output.WriteLine($"PAC={pac}{result.Body.Data.PAC.Unit}, DAY_ENERGY={dayEnergy}{result.Body.Data.DAY_ENERGY?.Unit}");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetInverterInfo_ReturnsDeviceInfo()
    {
        var result = await svc.GetInverterInfo();
        Assert.NotNull(result);
        Assert.NotNull(result.Head);
        Assert.NotNull(result.Body?.Data);
        Assert.NotEmpty(result.Body.Data);

        var entry = result.Body.Data.First();
        _output.WriteLine($"Inverter {entry.Key}: CustomName={entry.Value.CustomName}, UniqueID={entry.Value.UniqueID}, State={entry.Value.InverterState}");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetActiveDeviceInfo_ReturnsDevices()
    {
        var result = await svc.GetActiveDeviceInfo();
        Assert.NotNull(result);
        Assert.NotNull(result.Head);
        Assert.NotNull(result.Body?.Data);

        _output.WriteLine($"Inverters={result.Body.Data.Inverter?.Count ?? 0}, Meters={result.Body.Data.Meter?.Count ?? 0}, Storage={result.Body.Data.Storage?.Count ?? 0}");
    }

    [Fact(Skip = "planned in 1.13")]
    [Trait("Category", "Integration")]
    public async Task GetMeterRealtimeData_ReturnsData()
    {
        var result = await svc.GetMeterRealtimeData();
        Assert.NotNull(result);
        Assert.NotNull(result.Head);
        Assert.NotNull(result.Body?.Data);

        foreach (var meter in result.Body.Data)
            _output.WriteLine($"Meter {meter.Key}: PowerReal_P_Sum={meter.Value.PowerReal_P_Sum}W, Voltage_AC_Phase_1={meter.Value.Voltage_AC_Phase_1}V");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetStorageRealtimeData_ReturnsData()
    {
        var result = await svc.GetStorageRealtimeData();
        Assert.NotNull(result);
        Assert.NotNull(result.Head);
        Assert.NotNull(result.Body);

        if (result.Body.Data is not null)
        {
            foreach (var storage in result.Body.Data)
                _output.WriteLine($"Storage {storage.Key}: SOC={storage.Value.Controller?.StateOfCharge_Relative}%, Capacity={storage.Value.Controller?.DesignedCapacity}Wh");
        }
        else
            _output.WriteLine("No storage devices returned (Body.Data is null)");
    }
}
