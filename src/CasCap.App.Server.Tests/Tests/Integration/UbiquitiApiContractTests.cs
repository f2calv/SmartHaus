using CasCap.Tests.Infrastructure;
using System.Net;
using System.Text;

namespace CasCap.Tests;

/// <summary>Exercises webhook HTTP contracts with real parsing and an in-memory sink.</summary>
[Trait("Category", "Integration")]
public sealed class UbiquitiApiContractTests(DeviceApiWebApplicationFactory factory)
    : IClassFixture<DeviceApiWebApplicationFactory>
{
    [Theory]
    [InlineData("GET", "motion", UbiquitiEventType.Motion)]
    [InlineData("POST", "motion", UbiquitiEventType.Motion)]
    [InlineData("GET", "smart?type=PeRsOn", UbiquitiEventType.SmartDetectPerson)]
    [InlineData("POST", "smart?type=PeRsOn", UbiquitiEventType.SmartDetectPerson)]
    [InlineData("GET", "ring", UbiquitiEventType.Ring)]
    [InlineData("POST", "ring", UbiquitiEventType.Ring)]
    public async Task WebhookRoutes_AcceptOptionalBodiesAndForwardCancellation(string method, string route, UbiquitiEventType expected)
    {
        using var client = factory.CreateClient();
        var before = factory.CameraEvents.Count;
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/v1/Ubiquiti/event/{route}");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("\"ok\"", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(before + 1, factory.CameraEvents.Count);
        Assert.Equal(expected, factory.CameraEvents[^1].Event.UbiquitiEventType);
        Assert.True(factory.CameraEvents[^1].Token.CanBeCanceled);
    }

    [Theory]
    [InlineData("motion")]
    [InlineData("smart?type=person")]
    public async Task InvalidThumbnail_ReturnsPayloadRejection(string route)
    {
        using var client = factory.CreateClient();
        var before = factory.CameraEvents.Count;
        using var content = new StringContent("{\"alarm\":{\"thumbnail\":\"invalid-base64!\"}}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync($"/api/v1/Ubiquiti/event/{route}", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("\"Invalid UniFi Protect webhook payload.\"", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(before, factory.CameraEvents.Count);
    }

    [Theory]
    [InlineData("?type=unsupported", false)]
    [InlineData("", true)]
    public async Task InvalidDetection_ReturnsExplicitOrAutomaticValidation(string query, bool automatic)
    {
        using var client = factory.CreateClient();
        var before = factory.CameraEvents.Count;
        using var response = await client.GetAsync($"/api/v1/Ubiquiti/event/smart{query}", TestContext.Current.CancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, factory.CameraEvents.Count);
        if (automatic)
        {
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("type", out _));
        }
        else
            Assert.Equal("Unknown smart detection type 'unsupported'. Expected: person, vehicle, animal, package.", body.RootElement.GetString());
    }

    [Fact]
    public async Task OpenApi_DescribesBothWebhookMethodsAndOutcomes()
    {
        using var client = factory.CreateAuthorizedClient();
        using var response = await client.GetAsync("/swagger/v1.json", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var paths = document.RootElement.GetProperty("paths");

        foreach (var route in new[] { "motion", "smart", "ring" })
        {
            var path = Assert.Single(paths.EnumerateObject(), property =>
                property.Name.Equals($"/api/v1/Ubiquiti/event/{route}", StringComparison.OrdinalIgnoreCase)).Value;
            foreach (var method in new[] { "get", "post" })
            {
                var responses = path.GetProperty(method).GetProperty("responses");
                Assert.Equal("string", responses.GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("type").GetString());
                Assert.True(responses.TryGetProperty("400", out _));
            }
        }
    }
}
