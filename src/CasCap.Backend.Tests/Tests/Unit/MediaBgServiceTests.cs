using System.Globalization;

namespace CasCap.Tests.Unit;

/// <summary>Verifies media events are translated into stateless Agent Runtime requests.</summary>
[Trait("Category", "Media")]
public sealed class MediaBgServiceTests
{
    [Theory]
    [InlineData(MediaType.Image, null, "image/jpeg", true)]
    [InlineData(MediaType.Audio, null, "audio/wav", true)]
    [InlineData(MediaType.Document, "application/pdf", null, false)]
    public void CreateRunRequest_MapsMediaPayload(
        MediaType mediaType,
        string? configuredMimeType,
        string? expectedMimeType,
        bool expectsBinaryContent)
    {
        var content = new byte[] { 1, 2, 3 };
        var mediaEvent = new MediaEvent
        {
            Source = "ExampleSource",
            EventType = "ExampleEvent",
            Media = new MediaReference
            {
                MediaRedisKey = "media:test",
                MimeType = configuredMimeType,
            },
            MediaType = mediaType,
            TimestampUtc = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
        };

        var request = MediaBgService.CreateRunRequest(mediaEvent, content);

        Assert.StartsWith("media-", request.SessionId, StringComparison.Ordinal);
        Assert.True(request.BypassSession);
        Assert.Equal(expectedMimeType, request.MimeType);
        if (expectsBinaryContent)
            Assert.Equal(content, request.BinaryContent);
        else
        {
            Assert.Null(request.BinaryContent);
            Assert.Contains(content.Length.ToString(CultureInfo.InvariantCulture), request.Input, StringComparison.Ordinal);
        }
    }
}
