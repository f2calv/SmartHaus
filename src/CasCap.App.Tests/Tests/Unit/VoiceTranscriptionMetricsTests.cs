using System.Diagnostics.Metrics;

namespace CasCap.Tests.Unit;

/// <summary>
/// Verifies <see cref="VoiceTranscriptionMetrics"/> publishes the expected instruments and keeps the
/// dimension set bounded.
/// </summary>
[Trait("Category", "SpeechToText")]
public class VoiceTranscriptionMetricsTests
{
    [Fact]
    public void Record_SuccessPublishesEveryStage()
    {
        var measurements = Collect(m => m.Record(VoiceTranscriptionResult.Success("hello",
            audioDuration: TimeSpan.FromSeconds(4),
            transcodeDuration: TimeSpan.FromSeconds(2),
            transcriptionDuration: TimeSpan.FromSeconds(1))));

        Assert.Equal(4, Value(measurements, "haus.voice.audio.duration"));
        Assert.Equal(2, Value(measurements, "haus.voice.transcode.duration"));
        Assert.Equal(1, Value(measurements, "haus.voice.transcription.duration"));
        //Four seconds of audio normalised in two is twice realtime; transcribed in one is four times.
        Assert.Equal(2, Value(measurements, "haus.voice.transcode.speed"));
        Assert.Equal(4, Value(measurements, "haus.voice.transcription.speed"));
        Assert.Equal(1, Value(measurements, "haus.voice.transcriptions"));
    }

    [Fact]
    public void Record_FailureStillCountsTheOutcome()
    {
        var measurements = Collect(m =>
            m.Record(VoiceTranscriptionResult.Failure(VoiceTranscriptionOutcome.BackendFailed)));

        var counter = Assert.Single(measurements, x => x.Instrument == "haus.voice.transcriptions");
        Assert.Equal(1, counter.Value);
        var tag = Assert.Single(counter.Tags);
        Assert.Equal(VoiceTranscriptionMetrics.OutcomeTagName, tag.Key);
        Assert.Equal(nameof(VoiceTranscriptionOutcome.BackendFailed), tag.Value);
        //No stage ran, so no duration or speed may be reported.
        Assert.Single(measurements);
    }

    [Fact]
    public void Record_SkippedTranscodeIsNotReported()
    {
        var measurements = Collect(m => m.Record(VoiceTranscriptionResult.Success("hello",
            audioDuration: TimeSpan.FromSeconds(4),
            transcriptionDuration: TimeSpan.FromSeconds(1))));

        Assert.DoesNotContain(measurements, x => x.Instrument.StartsWith("haus.voice.transcode", StringComparison.Ordinal));
    }

    #region Private helpers

    private static double Value(List<Measured> measurements, string instrument) =>
        Assert.Single(measurements, x => x.Instrument == instrument).Value;

    private static List<Measured> Collect(Action<VoiceTranscriptionMetrics> act)
    {
        var metrics = TestMetrics.Voice();
        var measurements = new List<Measured>();

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Name.StartsWith("haus.voice.", StringComparison.Ordinal))
                    l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Add(new(instrument.Name, value, tags.ToArray())));
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add(new(instrument.Name, value, tags.ToArray())));
        listener.Start();

        act(metrics);
        listener.RecordObservableInstruments();

        return measurements;
    }

    private sealed record Measured(string Instrument, double Value, KeyValuePair<string, object?>[] Tags);

    #endregion
}
