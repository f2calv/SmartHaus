namespace CasCap.Extensions;

/// <summary>
/// Extension methods for generating LLM/Agent translation glossaries from
/// <see cref="KnxTranslationLanguage"/> configuration.
/// </summary>
public static class TranslationExtensions
{
    /// <summary>
    /// Builds a Markdown glossary block suitable for injection into an LLM/Agent system prompt.
    /// The glossary maps English enum values to their localised equivalents so that end users
    /// can query the system in their native language while all MCP tools use English internally.
    /// </summary>
    /// <param name="translations">
    /// The translation dictionaries keyed by language code (e.g. <c>de</c>).
    /// Sourced from <see cref="KnxConfig.Translations"/>.
    /// </param>
    /// <returns>
    /// A Markdown string containing a terminology glossary table, or an empty string
    /// when <paramref name="translations"/> is empty.
    /// </returns>
    public static string BuildTranslationGlossary(this Dictionary<string, KnxTranslationLanguage> translations)
    {
        if (translations.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("## KNX Terminology Glossary");
        sb.AppendLine();
        sb.AppendLine("The MCP tools use English values for rooms, floors, orientations and categories.");
        sb.AppendLine("When the user communicates in another language, map their input to the English value before calling a tool.");
        sb.AppendLine();

        foreach (var (lang, translation) in translations)
        {
            sb.AppendLine($"### Language: {lang}");
            sb.AppendLine();

            if (translation.Floors.Count > 0)
            {
                sb.AppendLine("#### Floors");
                sb.AppendLine();
                sb.AppendLine("| English | Local |");
                sb.AppendLine("| --- | --- |");
                foreach (var (english, local) in translation.Floors)
                    sb.AppendLine($"| {english} | {local} |");
                sb.AppendLine();
            }

            if (translation.Rooms.Count > 0)
            {
                sb.AppendLine("#### Rooms");
                sb.AppendLine();
                sb.AppendLine("| English | Local |");
                sb.AppendLine("| --- | --- |");
                foreach (var (english, local) in translation.Rooms)
                    sb.AppendLine($"| {english} | {local} |");
                sb.AppendLine();
            }

            if (translation.Orientations.Count > 0)
            {
                sb.AppendLine("#### Orientations");
                sb.AppendLine();
                sb.AppendLine("| English | Local |");
                sb.AppendLine("| --- | --- |");
                foreach (var (english, local) in translation.Orientations)
                    sb.AppendLine($"| {english} | {local} |");
                sb.AppendLine();
            }

            if (translation.Categories.Count > 0)
            {
                sb.AppendLine("#### Categories");
                sb.AppendLine();
                sb.AppendLine("| English | Local |");
                sb.AppendLine("| --- | --- |");
                foreach (var (english, local) in translation.Categories)
                    sb.AppendLine($"| {english} | {local} |");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
