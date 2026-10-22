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
}
