using CasCap.Tests.Infrastructure;
using System.Net;

namespace CasCap.Tests;

/// <summary>Checks device request binding and cancellation without live devices or storage.</summary>
[Trait("Category", "Integration")]
public sealed class DeviceApiContractTests(DeviceApiWebApplicationFactory factory)
    : IClassFixture<DeviceApiWebApplicationFactory>
{
    [Theory]
    [InlineData("/api/v1/Buderus/values")]
    [InlineData("/api/v1/Buderus/example")]
    [InlineData("/api/v1/Fronius/readings")]
    [InlineData("/api/v1/EdgeHardware/events")]
    [InlineData("/api/v1/Sicce/readings")]
    [InlineData("/api/v1/Shelly/readings")]
    public async Task HistoryRequests_ForwardRequestCancellation(string path)
    {
        using var client = factory.CreateClient();
        var before = factory.HistoryTokens.Count;
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        Assert.Equal("[]", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(before + 1, factory.HistoryTokens.Count);
        Assert.True(factory.HistoryTokens[^1].CanBeCanceled);
    }

    [Theory]
    [InlineData("POST", "/api/v1/Shelly/relay/example")]
    [InlineData("PUT", "/api/v1/Wiz/bulbs/example/power")]
    [InlineData("POST", "/api/v1/Shelly/relay/example?on=invalid")]
    [InlineData("PUT", "/api/v1/Wiz/bulbs/example/power?on=invalid")]
    public async Task MissingOrInvalidPowerState_DoesNotInvokeDevice(string method, string path)
    {
        using var client = factory.CreateClient();
        var before = factory.PowerCommands.Count;
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("on", out _));
        Assert.Equal(before, factory.PowerCommands.Count);
    }

    [Theory]
    [InlineData("POST", "/api/v1/Shelly/relay/example?on=false", false)]
    [InlineData("PUT", "/api/v1/Wiz/bulbs/example/power?on=false", false)]
    [InlineData("POST", "/api/v1/Shelly/relay/example?on=true", true)]
    [InlineData("PUT", "/api/v1/Wiz/bulbs/example/power?on=true", true)]
    public async Task ExplicitPowerState_IsForwarded(string method, string path, bool expected)
    {
        using var client = factory.CreateClient();
        var before = factory.PowerCommands.Count;
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        Assert.Equal(before + 1, factory.PowerCommands.Count);
        Assert.Equal(expected, factory.PowerCommands[^1]);
    }

    [Fact]
    public async Task OpenApi_DescribesDeviceSuccessAndValidation()
    {
        using var client = factory.CreateAuthorizedClient();
        using var response = await client.GetAsync("/swagger/v1.json", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var paths = body.RootElement.GetProperty("paths");

        var shelly = Assert.Single(paths.EnumerateObject(), path =>
            path.Name.Equals("/api/v1/Shelly/relay/{deviceId}", StringComparison.OrdinalIgnoreCase));
        var wiz = Assert.Single(paths.EnumerateObject(), path =>
            path.Name.Equals("/api/v1/Wiz/bulbs/{bulbIdentifier}/power", StringComparison.OrdinalIgnoreCase));
        var buderus = Assert.Single(paths.EnumerateObject(), path =>
            path.Name.Equals("/api/v1/Buderus/values", StringComparison.OrdinalIgnoreCase));
        Assert.True(shelly.Value.GetProperty("post")
            .GetProperty("responses").TryGetProperty("400", out _));
        Assert.True(wiz.Value.GetProperty("put")
            .GetProperty("responses").TryGetProperty("400", out _));
        Assert.True(buderus.Value.GetProperty("get")
            .GetProperty("responses").GetProperty("200").GetProperty("content")
            .TryGetProperty("application/json", out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DataPointWrite_MapsServiceOutcome(bool rejected)
    {
        factory.DataPointResult = new BuderusDataPointWriteResult { Error = rejected ? "Synthetic device rejection." : null };
        using var client = factory.CreateClient();
        using var content = new StringContent("{\"value\":\"50\"}", System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PutAsync("/api/v1/Buderus/example/value", content, TestContext.Current.CancellationToken);

        Assert.Equal(rejected ? HttpStatusCode.BadRequest : HttpStatusCode.OK, response.StatusCode);
        var command = factory.DataPointCommands[^1];
        Assert.Equal("example/value", command.Id);
        Assert.Equal("50", command.Value);
        Assert.True(command.Token.CanBeCanceled);
        if (rejected)
            Assert.Equal(factory.DataPointResult.Error,
                JsonSerializer.Deserialize<string>(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
    }

    [Theory]
    [InlineData("{\"dimming\":9}")]
    [InlineData("{\"dimming\":101}")]
    [InlineData("{\"r\":-1}")]
    [InlineData("{\"r\":256}")]
    [InlineData("{\"g\":-1}")]
    [InlineData("{\"g\":256}")]
    [InlineData("{\"b\":-1}")]
    [InlineData("{\"b\":256}")]
    [InlineData("{\"c\":-1}")]
    [InlineData("{\"c\":256}")]
    [InlineData("{\"w\":-1}")]
    [InlineData("{\"w\":256}")]
    [InlineData("{\"speed\":19}")]
    [InlineData("{\"speed\":201}")]
    public async Task InvalidPilotRanges_DoNotInvokeDevice(string json)
    {
        using var client = factory.CreateClient();
        var before = factory.PilotCommands.Count;
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PutAsync("/api/v1/Wiz/bulbs/example/pilot", content, TestContext.Current.CancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(body.RootElement.GetProperty("errors").EnumerateObject());
        Assert.Equal(before, factory.PilotCommands.Count);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"dimming\":10}")]
    [InlineData("{\"dimming\":100}")]
    [InlineData("{\"r\":0}")]
    [InlineData("{\"r\":255}")]
    [InlineData("{\"g\":0}")]
    [InlineData("{\"g\":255}")]
    [InlineData("{\"b\":0}")]
    [InlineData("{\"b\":255}")]
    [InlineData("{\"c\":0}")]
    [InlineData("{\"c\":255}")]
    [InlineData("{\"w\":0}")]
    [InlineData("{\"w\":255}")]
    [InlineData("{\"speed\":20}")]
    [InlineData("{\"speed\":200}")]
    public async Task PilotRanges_AcceptBoundariesAndOmittedOptionalValues(string json)
    {
        using var client = factory.CreateClient();
        var before = factory.PilotCommands.Count;
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PutAsync("/api/v1/Wiz/bulbs/example/pilot", content, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        Assert.Equal(before + 1, factory.PilotCommands.Count);
    }
}
