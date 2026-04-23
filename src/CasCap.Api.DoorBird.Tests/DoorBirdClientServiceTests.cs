namespace CasCap.Tests;

/// <summary>
/// Integration tests for <see cref="DoorBirdClientService"/> against a real DoorBird device.
/// </summary>
public class DoorBirdClientServiceTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task GetSession_ReturnsSessionResponse()
    {
        var result = await svc.GetSession();
        Assert.NotNull(result);
        Assert.NotNull(result.Bha);
        _output.WriteLine($"SessionId={result.Bha.SessionId}, ReturnCode={result.Bha.ReturnCode}");
    }

    [Fact]
    public async Task InvalidateSession_InvalidatesExistingSession()
    {
        var session = await svc.GetSession();
        Assert.NotNull(session?.Bha?.SessionId);

        var result = await svc.InvalidateSession(session.Bha.SessionId);
        Assert.NotNull(result);
        _output.WriteLine($"Invalidated session, ReturnCode={result.Bha.ReturnCode}");
    }

    [Fact]
    public async Task GetImage_ReturnsJpegBytes()
    {
        var bytes = await svc.GetImage();
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        _output.WriteLine($"Image size: {bytes.Length} bytes");
    }

    [Fact]
    public void GetVideoStreamUrl_ReturnsValidUri()
    {
        var uri = svc.GetVideoStreamUrl();
        Assert.NotNull(uri);
        Assert.Contains("video.cgi", uri.ToString());
        _output.WriteLine($"Video URL: {uri}");
    }

    [Fact]
    public async Task TriggerRelay_ReturnsTrue()
    {
        var result = await svc.TriggerRelay(_config.DoorControllerID, _config.DoorControllerRelayID);
        Assert.True(result);
        _output.WriteLine("Relay triggered successfully");
    }

    [Fact]
    public async Task LightOn_ReturnsResponse()
    {
        var result = await svc.LightOn();
        Assert.NotNull(result);
        Assert.NotNull(result.Bha);
        _output.WriteLine($"LightOn ReturnCode={result.Bha.ReturnCode}");
    }

    [Fact]
    public async Task GetInfo_ReturnsDeviceInfo()
    {
        var result = await svc.GetInfo();
        Assert.NotNull(result);
        Assert.NotNull(result.Bha);
        Assert.NotNull(result.Bha.Version);
        Assert.NotEmpty(result.Bha.Version);

        var version = result.Bha.Version[0];
        _output.WriteLine($"Firmware={version.Firmware}, BuildNumber={version.BuildNumber}, DeviceType={version.DeviceType}");
    }

    [Fact]
    public async Task GetSipStatus_ReturnsSipConfig()
    {
        var result = await svc.GetSipStatus();
        Assert.NotNull(result);
        Assert.NotNull(result.Bha);
        _output.WriteLine($"SIP Enabled={result.Bha.Enable}, IncomingCallEnable={result.Bha.IncomingCallEnable}");
    }

    // Note: Restart is intentionally excluded — it reboots the device.

    [Fact]
    public async Task GetFavorites_ReturnsString()
    {
        var result = await svc.GetFavorites();
        Assert.NotNull(result);
        _output.WriteLine($"Favorites: {result}");
    }

    [Fact]
    public async Task GetSchedule_ReturnsString()
    {
        var result = await svc.GetSchedule();
        Assert.NotNull(result);
        _output.WriteLine($"Schedule: {result}");
    }

    [Fact]
    public async Task GetHistoryImage_ReturnsJpegBytes()
    {
        var bytes = await svc.GetHistoryImage(index: 1);
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        _output.WriteLine($"History image size: {bytes.Length} bytes");
    }

    [Fact]
    public async Task GetHistoryImage_WithEventType_ReturnsJpegBytes()
    {
        var bytes = await svc.GetHistoryImage(index: 1, doorBirdEventType: DoorBirdEventType.Doorbell);
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        _output.WriteLine($"Doorbell history image size: {bytes.Length} bytes");
    }

    [Fact]
    public async Task GetHistoryImage_InvalidIndex_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => svc.GetHistoryImage(index: 0));
        await Assert.ThrowsAsync<ArgumentException>(() => svc.GetHistoryImage(index: 51));
    }

    [Fact]
    public async Task ListNotifications_ReturnsSubscribers()
    {
        var result = await svc.ListNotifications();
        Assert.NotNull(result);
        Assert.NotNull(result.Bha);
        _output.WriteLine($"Notification subscribers: {result.Bha.Notifications?.Length ?? 0}");
    }

    [Fact]
    public async Task SubscribeAndUnsubscribe_RoundTrip()
    {
        var testUrl = "http://192.168.1.100:9999/test-doorbird-callback";
        var eventType = "doorbell";

        var subscribed = await svc.SubscribeNotification(testUrl, eventType);
        Assert.True(subscribed);
        _output.WriteLine("Subscribed successfully");

        var unsubscribed = await svc.UnsubscribeNotification(testUrl, eventType);
        Assert.True(unsubscribed);
        _output.WriteLine("Unsubscribed successfully");
    }
}
