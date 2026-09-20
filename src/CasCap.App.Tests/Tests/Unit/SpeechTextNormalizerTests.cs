namespace CasCap.Tests.Unit;

/// <summary>
/// Conversion tests for <see cref="SpeechTextNormalizer"/>.
/// </summary>
[Trait("Category", "TextToSpeech")]
public class SpeechTextNormalizerTests
{
    //The reply that was actually spoken as "asterisk asterisk 24 windows".
    [Fact]
    public void RealReply_LosesMarkupAndGainsSentenceBreaks()
    {
        const string markdown = """
            **24 windows/shutters are open** in your house:
            - 20 fully open
            - 4 partially open
            """;

        var result = SpeechTextNormalizer.ToSpeakable(markdown);

        Assert.Equal("24 windows/shutters are open in your house: 20 fully open. 4 partially open.", result);
        Assert.DoesNotContain('*', result);
    }

    [Theory]
    [InlineData("**bold**", "bold")]
    [InlineData("*italic*", "italic")]
    [InlineData("__bold__", "bold")]
    [InlineData("_italic_", "italic")]
    [InlineData("***both***", "both")]
    public void EmphasisMarkersAreRemoved(string markdown, string expected)
    {
        Assert.Equal($"{expected}.", SpeechTextNormalizer.ToSpeakable(markdown));
    }

    [Fact]
    public void HeadingsLoseTheirHashes()
    {
        Assert.Equal("Status.", SpeechTextNormalizer.ToSpeakable("## Status"));
    }

    [Fact]
    public void LinksAreSpokenAsTheirText()
    {
        Assert.Equal("the dashboard.",
            SpeechTextNormalizer.ToSpeakable("[the dashboard](https://example.com/very/long/url)"));
    }

    [Fact]
    public void InlineCodeKeepsItsContent()
    {
        Assert.Equal("run deploy now.", SpeechTextNormalizer.ToSpeakable("run `deploy` now"));
    }

    [Fact]
    public void FencedCodeIsDropped()
    {
        var result = SpeechTextNormalizer.ToSpeakable("Here you go:\n```\nvar x = 1;\n```\nDone");

        Assert.DoesNotContain("var x", result, StringComparison.Ordinal);
        Assert.Contains("Done", result, StringComparison.Ordinal);
    }

    //Each bullet becomes its own sentence, which is what produces the pause between items.
    [Fact]
    public void EveryBulletBecomesASentence()
    {
        var result = SpeechTextNormalizer.ToSpeakable("- one\n- two\n- three");

        Assert.Equal("one. two. three.", result);
    }

    [Fact]
    public void ExistingTerminalPunctuationIsNotDoubled()
    {
        Assert.Equal("All done!", SpeechTextNormalizer.ToSpeakable("All done!"));
    }

    [Fact]
    public void NumberedItemsKeepTheirNumbering()
    {
        Assert.Equal("1. open the door. 2. close it.",
            SpeechTextNormalizer.ToSpeakable("1. open the door\n2. close it"));
    }

    [Fact]
    public void HorizontalRulesAreDropped()
    {
        Assert.Equal("before. after.", SpeechTextNormalizer.ToSpeakable("before\n---\nafter"));
    }

    [Fact]
    public void TableRowsGainSeparators()
    {
        var result = SpeechTextNormalizer.ToSpeakable("| room | state |\n| --- | --- |\n| hall | open |");

        Assert.DoesNotContain('|', result);
        Assert.Contains("hall, open", result, StringComparison.Ordinal);
    }

    [Fact]
    public void EmojiAreNotSpoken()
    {
        var result = SpeechTextNormalizer.ToSpeakable("All good \U0001F44D \u2705");

        Assert.Equal("All good.", result);
    }

    //Degrees and currency read correctly, so they must survive the emoji pass.
    [Fact]
    public void TextSymbolsSurvive()
    {
        Assert.Equal("It is 21\u00B0C and costs \u00A35.", SpeechTextNormalizer.ToSpeakable("It is 21\u00B0C and costs \u00A35"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("**")]
    [InlineData("---")]
    public void NothingToSayReturnsEmpty(string? markdown)
    {
        Assert.Equal(string.Empty, SpeechTextNormalizer.ToSpeakable(markdown));
    }

    [Fact]
    public void BlockQuotesLoseTheirMarker()
    {
        Assert.Equal("quoted text.", SpeechTextNormalizer.ToSpeakable("> quoted text"));
    }
}
