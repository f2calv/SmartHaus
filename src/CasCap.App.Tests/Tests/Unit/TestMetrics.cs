using System.Diagnostics.Metrics;

namespace CasCap.Tests.Unit;

/// <summary>Builds real instrument instances for tests that do not assert on measurements.</summary>
internal static class TestMetrics
{
    /// <summary>Creates a <see cref="VoiceTranscriptionMetrics"/> backed by a throwaway meter factory.</summary>
    public static VoiceTranscriptionMetrics Voice()
    {
        var config = Options.Create(new VoiceMetricsConfig
        {
            MetricNamePrefix = "haus",
        });
        var meterFactory = new ServiceCollection().AddMetrics().BuildServiceProvider()
            .GetRequiredService<IMeterFactory>();
        return new(config, Options.Create(new SpeechToTextConfig()), meterFactory);
    }
}
