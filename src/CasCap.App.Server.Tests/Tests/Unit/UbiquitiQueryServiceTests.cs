using CasCap.Models.Dtos;
using Microsoft.Extensions.Logging.Abstractions;

namespace CasCap.Tests;

/// <summary>Checks webhook parsing and dispatch using in-memory sinks only.</summary>
[Trait("Category", "Webhook Parsing")]
public sealed class UbiquitiQueryServiceTests
{
    [Theory]
    [InlineData("person", UbiquitiEventType.SmartDetectPerson)]
    [InlineData("PeRsOn", UbiquitiEventType.SmartDetectPerson)]
    [InlineData("vehicle", UbiquitiEventType.SmartDetectVehicle)]
    [InlineData("VEHICLE", UbiquitiEventType.SmartDetectVehicle)]
    [InlineData("animal", UbiquitiEventType.SmartDetectAnimal)]
    [InlineData("Animal", UbiquitiEventType.SmartDetectAnimal)]
    [InlineData("package", UbiquitiEventType.SmartDetectPackage)]
    [InlineData("PACKAGE", UbiquitiEventType.SmartDetectPackage)]
    public async Task SmartDetection_ParsesSupportedTypes(string type, UbiquitiEventType expected)
    {
        var sink = new RecordingSink();
        var webhook = new UbiquitiWebhookRequest
        {
            Timestamp = 1000,
            Alarm = new() { Thumbnail = "data:image/png;base64,AQID" },
        };

        var result = await CreateService(sink).SendSmartDetectionAlert(
            type, "camera-example", "Example", 0.9, webhook, TestContext.Current.CancellationToken);

        Assert.Null(result.Error);
        var alert = Assert.Single(sink.Events);
        Assert.Equal(expected, alert.UbiquitiEventType);
        Assert.Equal("camera-example", alert.SourceCameraId);
        Assert.Equal("***mple", alert.CameraId);
        Assert.Equal("Example", alert.CameraName);
        Assert.Equal(0.9, alert.Score);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1000).UtcDateTime, alert.DateCreatedUtc);
        Assert.Equal(new byte[] { 1, 2, 3 }, alert.Thumbnail);
        Assert.Equal("image/png", alert.ThumbnailMimeType);
        Assert.Equal(TestContext.Current.CancellationToken, sink.Token);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unsupported")]
    public async Task UnknownDetection_DoesNotDispatch(string type)
    {
        var sink = new RecordingSink();

        var result = await CreateService(sink).SendSmartDetectionAlert(type, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal($"Unknown smart detection type '{type}'. Expected: person, vehicle, animal, package.", result.Error);
        Assert.Empty(sink.Events);
    }

    [Theory]
    [InlineData("invalid-base64!", null)]
    [InlineData("data:image/jpeg;base64", null)]
    [InlineData("data:image/jpeg,AQID", null)]
    [InlineData("data:audio/wav;base64,AQID", null)]
    [InlineData(null, long.MaxValue)]
    public async Task InvalidPayload_DoesNotDispatch(string? thumbnail, long? timestamp)
    {
        var sink = new RecordingSink();
        var webhook = new UbiquitiWebhookRequest { Timestamp = timestamp, Alarm = new() { Thumbnail = thumbnail } };

        var result = await CreateService(sink).SendAlert(
            UbiquitiEventType.Motion, webhook: webhook, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Invalid UniFi Protect webhook payload.", result.Error);
        Assert.Empty(sink.Events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CameraSelection_PreservesExplicitOverrideAndDeduplicatesTriggers(bool explicitCamera)
    {
        var sink = new RecordingSink();
        var result = await CreateService(sink).SendAlert(
            UbiquitiEventType.Motion, explicitCamera ? "camera-override" : null,
            webhook: TriggerPayload(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(result.Error);
        string[] expected = explicitCamera ? ["camera-override"] : ["camera-one", "camera-two"];
        Assert.Equal(expected, sink.Events.Select(alert => alert.SourceCameraId));
        Assert.All(sink.Events, alert => Assert.Equal(FixedClock.UtcNow.UtcDateTime, alert.DateCreatedUtc));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SinkFailures_AreNotPayloadRejections(bool cancelled)
    {
        Exception failure = cancelled ? new OperationCanceledException() : new FormatException("Synthetic sink failure.");
        var sink = new RecordingSink { Failure = failure };

        var thrown = await Assert.ThrowsAsync(failure.GetType(), async () =>
            await CreateService(sink).SendAlert(UbiquitiEventType.Motion, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Same(failure, thrown);
    }

    [Fact]
    public async Task CancelledRequest_DoesNotDispatch()
    {
        var sink = new RecordingSink();
        using var stopping = new CancellationTokenSource();
        await stopping.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateService(sink).SendAlert(UbiquitiEventType.Ring, cancellationToken: stopping.Token));

        Assert.Empty(sink.Events);
    }

    [Fact]
    public async Task CancellationBetweenCameras_StopsFurtherDispatch()
    {
        using var stopping = new CancellationTokenSource();
        var sink = new RecordingSink { AfterWrite = stopping.Cancel };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateService(sink).SendAlert(UbiquitiEventType.Motion, webhook: TriggerPayload(), cancellationToken: stopping.Token));

        Assert.Single(sink.Events);
        Assert.Equal(stopping.Token, sink.Token);
    }

    private static UbiquitiQueryService CreateService(RecordingSink sink)
        => new(NullLogger<UbiquitiQueryService>.Instance, new FixedClock(), [sink]);

    private static UbiquitiWebhookRequest TriggerPayload()
        => new()
        {
            Alarm = new()
            {
                Triggers = [new() { Device = "camera-one" }, new() { Device = "camera-one" }, new() { Device = "camera-two" }],
            },
        };

    private sealed class FixedClock : TimeProvider
    {
        public static DateTimeOffset UtcNow { get; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class RecordingSink : IEventSink<UbiquitiEvent>
    {
        public string SinkType => "Recording";
        public List<UbiquitiEvent> Events { get; } = [];
        public CancellationToken Token { get; private set; }
        public Exception? Failure { get; init; }
        public Action? AfterWrite { get; init; }

        public Task WriteEvent(UbiquitiEvent @event, CancellationToken cancellationToken = default)
        {
            Token = cancellationToken;
            if (Failure is { } failure)
                return Task.FromException(failure);
            Events.Add(@event);
            AfterWrite?.Invoke();
            return Task.CompletedTask;
        }
    }
}
