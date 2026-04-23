namespace CasCap.Tests;

/// <summary>
/// Integration tests for <see cref="BuderusKm200ClientService"/> against a real Buderus KM200 device.
/// </summary>
/// <remarks>
/// All tests require a reachable KM200 device configured via <c>appsettings.json</c> or
/// <c>appsettings.Development.json</c> under the <c>CasCap:BuderusConfig</c> key.
/// Tests are skipped by default to avoid failures in CI environments without hardware access.
/// </remarks>
public class BuderusKm200ClientServiceTests(ITestOutputHelper output) : TestBase(output)
{
    #region Read datapoints

    [Fact]
    public async Task GetDataPoint_DhwSetTemperature_ReturnsValue()
    {
        var dp = await svc.GetDataPoint("/dhwCircuits/dhw1/setTemperature");

        Assert.NotNull(dp);
        Assert.Equal("/dhwCircuits/dhw1/setTemperature", dp.Id);
        Assert.Equal(MyDatapointType.floatValue, dp.Type);
        Assert.NotNull(dp.Value);

        _output.WriteLine($"DHW setTemperature = {dp.Value} {dp.UnitOfMeasure}");
    }

    [Fact]
    public async Task GetDataPoint_DhwActualTemp_ReturnsValue()
    {
        var dp = await svc.GetDataPoint("/dhwCircuits/dhw1/actualTemp");

        Assert.NotNull(dp);
        Assert.Equal(MyDatapointType.floatValue, dp.Type);

        _output.WriteLine($"DHW actualTemp = {dp.Value} {dp.UnitOfMeasure}");
    }

    [Fact]
    public async Task GetDataPoint_HeatingCircuit1NormalTemp_ReturnsValue()
    {
        var dp = await svc.GetDataPoint("/heatingCircuits/hc1/temperatureLevels/normal");

        Assert.NotNull(dp);
        Assert.Equal(MyDatapointType.floatValue, dp.Type);
        Assert.Equal(1, dp.Writeable);

        _output.WriteLine($"HC1 normal temperature = {dp.Value} {dp.UnitOfMeasure}");
    }

    [Fact]
    public async Task GetDataPoint_HeatingCircuit2NormalTemp_ReturnsValue()
    {
        var dp = await svc.GetDataPoint("/heatingCircuits/hc2/temperatureLevels/normal");

        Assert.NotNull(dp);
        Assert.Equal(MyDatapointType.floatValue, dp.Type);
        Assert.Equal(1, dp.Writeable);

        _output.WriteLine($"HC2 normal temperature = {dp.Value} {dp.UnitOfMeasure}");
    }

    [Fact]
    public async Task GetDataPoint_OperationMode_ReturnsStringValue()
    {
        var dp = await svc.GetDataPoint("/dhwCircuits/dhw1/operationMode");

        Assert.NotNull(dp);
        Assert.Equal(MyDatapointType.stringValue, dp.Type);
        Assert.NotNull(dp.Value);

        _output.WriteLine($"DHW operationMode = {dp.Value}");
    }

    [Fact]
    public async Task GetDataPoint_NonExistentPath_ReturnsNull()
    {
        var dp = await svc.GetDataPoint("/dhwCircuits/dhw99/setTemperature");

        Assert.Null(dp);
        _output.WriteLine("Non-existent datapoint correctly returned null");
    }

    [Fact]
    public async Task GetAllDataPoints_ReturnsCollection()
    {
        var dataPoints = await svc.GetAllDataPoints(CancellationToken.None);

        Assert.NotNull(dataPoints);
        Assert.NotEmpty(dataPoints);

        _output.WriteLine($"Retrieved {dataPoints.Count} datapoints in total");

        var writeable = dataPoints.FindAll(dp => dp.Writeable == 1);
        _output.WriteLine($"Writeable datapoints: {writeable.Count}");

        foreach (var dp in dataPoints.Take(10))
            _output.WriteLine($"  {dp.Id} ({dp.Type}) = {dp.Value}");
    }

    #endregion

    #region Write datapoints

    [Fact(Skip = "Requires a reachable Buderus KM200 device — changes live settings")]
    public async Task SetDataPoint_DhwSetTemperature_Roundtrip()
    {
        const string datapointId = "/dhwCircuits/dhw1/setTemperature";

        // Read current value
        var original = await svc.GetDataPoint(datapointId);
        Assert.NotNull(original);
        _output.WriteLine($"Original {datapointId} = {original.Value} {original.UnitOfMeasure}");

        // Write the same value back (no-op from the device's perspective)
        var success = await svc.SetDataPoint(datapointId, original.Value!);
        Assert.True(success, $"SetDataPoint returned false for '{datapointId}'");

        // Confirm the value is unchanged
        var after = await svc.GetDataPoint(datapointId);
        Assert.NotNull(after);
        Assert.Equal(original.Value?.ToString(), after.Value?.ToString());

        _output.WriteLine($"Roundtrip successful — {datapointId} still = {after.Value} {after.UnitOfMeasure}");
    }

    [Fact(Skip = "Requires a reachable Buderus KM200 device — changes live settings")]
    public async Task SetDataPoint_HeatingCircuit2NormalTemp_Roundtrip()
    {
        const string datapointId = "/heatingCircuits/hc2/temperatureLevels/normal";

        var original = await svc.GetDataPoint(datapointId);
        Assert.NotNull(original);
        Assert.Equal(1, original.Writeable);
        _output.WriteLine($"Original {datapointId} = {original.Value} {original.UnitOfMeasure}");

        var success = await svc.SetDataPoint(datapointId, original.Value!);
        Assert.True(success, $"SetDataPoint returned false for '{datapointId}'");

        var after = await svc.GetDataPoint(datapointId);
        Assert.NotNull(after);
        Assert.Equal(original.Value?.ToString(), after.Value?.ToString());

        _output.WriteLine($"Roundtrip successful — {datapointId} still = {after.Value} {after.UnitOfMeasure}");
    }

    [Fact]
    public async Task SetDataPoint_DhwOperationMode_Roundtrip()
    {
        const string datapointId = "/dhwCircuits/dhw1/operationMode";

        var original = await svc.GetDataPoint(datapointId);
        Assert.NotNull(original);
        Assert.Equal(MyDatapointType.stringValue, original.Type);
        Assert.Equal(1, original.Writeable);
        _output.WriteLine($"Original {datapointId} = {original.Value}");

        // Write the same value back
        var success = await svc.SetDataPoint(datapointId, original.Value!);
        Assert.True(success, $"SetDataPoint returned false for '{datapointId}'");

        var after = await svc.GetDataPoint(datapointId);
        Assert.NotNull(after);
        Assert.Equal(original.Value?.ToString(), after.Value?.ToString());

        _output.WriteLine($"Roundtrip successful — {datapointId} still = {after.Value}");
    }

    [Fact]
    public async Task SetDataPoint_ReadOnlyDatapoint_ReturnsFalse()
    {
        // /dhwCircuits/dhw1/actualTemp is not writeable (writeable=0)
        var success = await svc.SetDataPoint("/dhwCircuits/dhw1/actualTemp", "50");

        Assert.False(success, "SetDataPoint should return false for a read-only datapoint");
        _output.WriteLine("Correctly rejected write to read-only datapoint");
    }

    #endregion
}
