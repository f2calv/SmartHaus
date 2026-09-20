using System.Text;
using System.Text.RegularExpressions;

namespace CasCap.Services;

/// <summary>
/// Rewrites an agent reply written in Markdown into something worth listening to.
/// </summary>
/// <remarks>
/// A synthesizer reads Markdown literally, so <c>**24 windows**</c> becomes "asterisk asterisk 24
/// windows". Structure is therefore converted to punctuation rather than removed outright: each
/// bullet becomes its own sentence, which is what produces the pause between items.
/// <para>
/// The output is plain text because it feeds every provider. SSML break tags would give finer control
/// but are spoken literally by the OpenAI-compatible route, so any provider-specific prosody belongs
/// in that provider's adapter, not here.
/// </para>
/// <para>
/// TODO: this treats the symptom. The agent could instead be asked for speech-shaped prose whenever
/// the reply will be spoken, which reads better aloud than a flattened bullet list ever will, and
/// <c>InboundWasVoice</c> already reaches the agent call so the directive needs no new plumbing.
/// Three options, undecided:
/// <list type="number">
/// <item>A prompt directive with this kept as a deterministic guard. A small quantized local model
/// follows "no Markdown" most of the time, and the failure is audible, so a guard earns its place.</item>
/// <item>Ask the agent for a formatted reply and a spoken one. Keeps the readable bullet list the
/// text message currently gets, at the cost of tokens and latency.</item>
/// <item>A prompt directive alone, deleting this class and trusting the model.</item>
/// </list>
/// The unresolved trade-off is that one string is sent as both the message and the audio, so making
/// it speech-shaped also makes the readable reply plainer.
/// </para>
/// <para>
/// A dependency on a real Markdown parser was considered and backed out: regex is proportionate for a
/// guard, and would not be for a primary mechanism.
/// </para>
/// <para>
/// TODO: a slash between words is handled differently per provider, so any rule here would override
/// a sensible default rather than fix a fault everywhere. Azure AI Speech runs <c>windows/shutters</c>
/// together with no pause; Piper speaks it as "windows slash shutters", which is acceptable.
/// Substituting " or " would work only when both sides are alphabetic and at least two characters
/// long; a blanket replacement would mangle dates, paths, URLs and units, where <c>km/h</c> is saved
/// only by its single-character right side. Lower priority than it first appeared, and further
/// evidence for asking the agent for spoken prose instead.
/// </para>
/// </remarks>
public static partial class SpeechTextNormalizer
{
    /// <summary>Converts Markdown to a plain-text form suitable for synthesis.</summary>
    /// <param name="markdown">The reply as the agent composed it.</param>
    /// <returns>Speakable text, or an empty string when nothing is left to say.</returns>
    public static string ToSpeakable(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        var text = markdown;
        text = FencedCode().Replace(text, " ");
        text = InlineCode().Replace(text, "$1");
        text = Image().Replace(text, "$1");
        text = Link().Replace(text, "$1");

        var builder = new StringBuilder();
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Replace("\r", string.Empty);

            if (HorizontalRule().IsMatch(line) || TableSeparator().IsMatch(line))
                continue;

            line = Heading().Replace(line, string.Empty);
            line = BlockQuote().Replace(line, string.Empty);
            line = Bullet().Replace(line, string.Empty);
            //A table row reads as a run-on without separators between its cells.
            line = EdgePipe().Replace(line, string.Empty);
            line = InnerPipe().Replace(line, ", ");
            line = BoldOrItalic().Replace(line, "$2");
            line = Emoji().Replace(line, string.Empty);
            line = Whitespace().Replace(line, " ").Trim();
            line = StrandedPunctuation().Replace(line, string.Empty).Trim();

            if (line.Length == 0)
                continue;

            builder.Append(line);
            //Terminal punctuation is what the synthesizer turns into a pause, so every line that
            //  lacks it gets one; without this the bullets run together as a single breathless clause.
            if (!EndsSentence(line))
                builder.Append('.');
            builder.Append(' ');
        }

        return Whitespace().Replace(builder.ToString(), " ").Trim();
    }

    private static bool EndsSentence(string line) =>
        line.Length > 0 && line[^1] is '.' or '!' or '?' or ':' or ';' or ',';

    [GeneratedRegex(@"```.*?```", RegexOptions.Singleline)]
    private static partial Regex FencedCode();

    [GeneratedRegex(@"`([^`]*)`")]
    private static partial Regex InlineCode();

    [GeneratedRegex(@"!\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex Image();

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex Link();

    [GeneratedRegex(@"^\s*([-*_])(\s*\1){2,}\s*$")]
    private static partial Regex HorizontalRule();

    [GeneratedRegex(@"^\s*\|?[\s:|-]*\|[\s:|-]*$")]
    private static partial Regex TableSeparator();

    [GeneratedRegex(@"^\s{0,3}#{1,6}\s*")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^\s*>+\s?")]
    private static partial Regex BlockQuote();

    //Numbered items keep their number, which reads naturally; only the bullet glyph is dropped.
    [GeneratedRegex(@"^\s*[-*+]\s+")]
    private static partial Regex Bullet();

    [GeneratedRegex(@"(\*\*\*|\*\*|\*|___|__|_)(.*?)\1")]
    private static partial Regex BoldOrItalic();

    //Surrogate-pair pictographs plus the BMP symbol blocks; text symbols such as degree and currency
    //  are deliberately left alone because they are spoken correctly.
    [GeneratedRegex(@"[\uD83C-\uD83E][\uDC00-\uDFFF]|[\u2190-\u21FF\u2300-\u23FF\u25A0-\u27BF\u2B00-\u2BFF]|[\uFE0F\u200D\u20E3]")]
    private static partial Regex Emoji();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    //An emphasis or table marker left stranded once its partner was removed. Sentence punctuation is
    //  deliberately excluded: a trailing colon introduces the list that follows it.
    [GeneratedRegex(@"^[\s|*_]+|[\s|*_]+$")]
    private static partial Regex StrandedPunctuation();

    [GeneratedRegex(@"^\s*\||\|\s*$")]
    private static partial Regex EdgePipe();

    [GeneratedRegex(@"\s*\|\s*")]
    private static partial Regex InnerPipe();
}
