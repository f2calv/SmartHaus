namespace CasCap.Tests.Unit;

/// <summary>
/// Endpoint parsing tests for <see cref="PiperTextToSpeechClient"/>.
/// </summary>
[Trait("Category", "TextToSpeech")]
public class PiperEndpointTests
{
    [Theory]
    [InlineData("piper.example", "piper.example", 10200)]
    [InlineData("piper.example:10200", "piper.example", 10200)]
    [InlineData("piper.example:11000", "piper.example", 11000)]
    [InlineData("localhost:10200", "localhost", 10200)]
    [InlineData("10.0.0.5:10200", "10.0.0.5", 10200)]
    //A scheme is accepted but ignored, because operators reach for one out of habit.
    [InlineData("tcp://piper.example:10200", "piper.example", 10200)]
    [InlineData("https://piper.example:10200/", "piper.example", 10200)]
    public void ParseEndpoint_SplitsHostAndPort(string endpoint, string expectedHost, int expectedPort)
    {
        var (host, port) = PiperTextToSpeechClient.ParseEndpoint(endpoint);

        Assert.Equal(expectedHost, host);
        Assert.Equal(expectedPort, port);
    }

    [Fact]
    public void ParseEndpoint_NonNumericPortFallsBackToDefault()
    {
        var (host, port) = PiperTextToSpeechClient.ParseEndpoint("piper.example:notaport");

        Assert.Equal("piper.example:notaport", host);
        Assert.Equal(PiperTextToSpeechClient.DefaultPort, port);
    }
}
