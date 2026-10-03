namespace CasCap.Tests.Unit;

[Trait("Category", "CameraClips")]
public sealed class CameraClipQueueTests
{
    private const string CameraId = "CAMERA_A";

    [Fact]
    public void TryEnqueue_UnmappedCamera_IsNotConfigured()
    {
        var queue = CreateQueue(new CameraClipConfig { Enabled = true });

        var result = queue.TryEnqueue(CreateEvent(CameraId));

        Assert.Equal(CameraClipAdmission.NotConfigured, result);
    }

    [Fact]
    public void TryEnqueue_MappedCamera_QueuesLogicalSource()
    {
        var queue = CreateQueue(CreateConfig());

        var result = queue.TryEnqueue(CreateEvent(CameraId));

        Assert.Equal(CameraClipAdmission.Enqueued, result);
        Assert.True(queue.Reader.TryRead(out var request));
        Assert.Equal("camera-medium", request.Source.Path);
        Assert.Equal("ExampleCamera", request.Source.DisplayName);
    }

    [Fact]
    public void TryEnqueue_RepeatedCameraWithinCooldown_IsSuppressed()
    {
        var timeProvider = new MutableTimeProvider();
        var queue = CreateQueue(CreateConfig(cooldownSeconds: 30), timeProvider);

        Assert.Equal(CameraClipAdmission.Enqueued, queue.TryEnqueue(CreateEvent(CameraId)));
        Assert.Equal(CameraClipAdmission.Suppressed, queue.TryEnqueue(CreateEvent(CameraId)));

        timeProvider.Advance(TimeSpan.FromSeconds(31));
        Assert.Equal(CameraClipAdmission.Enqueued, queue.TryEnqueue(CreateEvent(CameraId)));
    }

    [Fact]
    public void TryEnqueue_FullQueue_ReturnsQueueFull()
    {
        var config = CreateConfig(queueCapacity: 1);
        config.Sources["CAMERA_B"] = new CameraClipSourceConfig
        {
            DisplayName = "SecondCamera",
            Path = "camera-two-medium",
        };
        var queue = CreateQueue(config);

        Assert.Equal(CameraClipAdmission.Enqueued, queue.TryEnqueue(CreateEvent(CameraId)));
        Assert.Equal(CameraClipAdmission.QueueFull, queue.TryEnqueue(CreateEvent("CAMERA_B")));
    }

    [Fact]
    public void TryEnqueue_ConfiguredDoorBird_QueuesFrontDoorSource()
    {
        var config = CreateConfig() with
        {
            DoorBirdSource = new CameraClipSourceConfig
            {
                DisplayName = "FrontDoor",
                Path = "doorbird",
            },
        };
        var queue = CreateQueue(config);
        var @event = new DoorBirdEvent
        {
            DoorBirdEventType = DoorBirdEventType.MotionSensor,
            DateCreatedUtc = new DateTime(2026, 10, 3, 8, 0, 0, DateTimeKind.Utc),
        };

        var result = queue.TryEnqueue(@event);

        Assert.Equal(CameraClipAdmission.Enqueued, result);
        Assert.True(queue.Reader.TryRead(out var request));
        Assert.Equal("doorbird", request.Source.Path);
        Assert.Equal("MotionSensor", request.EventType);
        Assert.Equal("DoorBird", request.MediaSource);
    }

    [Fact]
    public void TryEnqueue_DoorRelay_IsNotConfigured()
    {
        var config = CreateConfig() with
        {
            DoorBirdSource = new CameraClipSourceConfig
            {
                DisplayName = "FrontDoor",
                Path = "doorbird",
            },
        };
        var queue = CreateQueue(config);

        var result = queue.TryEnqueue(new DoorBirdEvent
        {
            DoorBirdEventType = DoorBirdEventType.DoorRelay,
        });

        Assert.Equal(CameraClipAdmission.NotConfigured, result);
    }

    [Fact]
    public void UbiquitiEvent_SerializedMetadataExcludesRawCameraId()
    {
        var json = CreateEvent(CameraId).ToJson();

        Assert.DoesNotContain(CameraId, json, StringComparison.Ordinal);
        Assert.Contains("***RA_A", json, StringComparison.Ordinal);
    }

    private static CameraClipQueue CreateQueue(
        CameraClipConfig config,
        TimeProvider? timeProvider = null)
        => new(Options.Create(config), timeProvider ?? new MutableTimeProvider());

    private static CameraClipConfig CreateConfig(
        int cooldownSeconds = 30,
        int queueCapacity = 32)
        => new()
        {
            Enabled = true,
            QueueCapacity = queueCapacity,
            Sources =
            {
                [CameraId] = new CameraClipSourceConfig
                {
                    DisplayName = "ExampleCamera",
                    Path = "camera-medium",
                    CooldownSeconds = cooldownSeconds,
                },
            },
        };

    private static UbiquitiEvent CreateEvent(string cameraId)
        => new()
        {
            UbiquitiEventType = UbiquitiEventType.Motion,
            DateCreatedUtc = new DateTime(2026, 10, 3, 8, 0, 0, DateTimeKind.Utc),
            SourceCameraId = cameraId,
            CameraId = $"***{cameraId[^4..]}",
        };

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow =
            new(2026, 10, 3, 8, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
