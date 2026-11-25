using System.Text.RegularExpressions;

namespace FWO.Ai.Client.ReasonChecks;

/// <summary>
/// Regular expressions of the reason checks. All patterns run in linear time; the match timeout is only a safeguard.
/// </summary>
internal static partial class ReasonCheckPatterns
{
    /// <summary>
    /// Pattern of a language name: letters, spaces and hyphens, starting with a letter. \A and \z anchor the whole
    /// text, unlike $, which also matches before a final line break. Keep in step with RequestApplicationPrompts of
    /// the MCP server.
    /// </summary>
    internal const string kLanguagePattern = @"\A\p{L}[\p{L} \-]*\z";

    /// <summary>Longest time a match may take.</summary>
    private const int kMatchTimeoutMilliseconds = 1000;

    /// <summary>
    /// Pattern of a verdict line, e.g. "Verdict: GENUINE" or "1. **Verdict:** NOT_GENUINE": a line whose first word is
    /// "Verdict", followed by a verdict word after nothing but markup, punctuation and blanks. Case-insensitive.
    /// </summary>
    private const string kVerdictLinePattern =
        @"^[^\p{L}\r\n]*verdict[^\p{L}\r\n]*(?<verdict>NOT[ _\-]?GENUINE|INSUFFICIENT|GENUINE)\b";

    /// <summary>
    /// Pattern of a standalone verdict word in upper case. \b treats _ as a word character, so GENUINE is not found
    /// inside NOT_GENUINE; NOT_GENUINE is tried first, so that "NOT GENUINE" is not read as GENUINE.
    /// </summary>
    private const string kVerdictWordPattern = @"\b(?<verdict>NOT[ _\-]?GENUINE|INSUFFICIENT|GENUINE)\b";

    /// <summary>Name of the group holding the verdict word.</summary>
    internal const string kVerdictGroup = "verdict";

    /// <summary>Matches a language name, see <see cref="kLanguagePattern"/>.</summary>
    [GeneratedRegex(kLanguagePattern, RegexOptions.CultureInvariant, kMatchTimeoutMilliseconds)]
    internal static partial Regex LanguageRegex();

    /// <summary>Matches a verdict line, see <see cref="kVerdictLinePattern"/>.</summary>
    [GeneratedRegex(kVerdictLinePattern, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant,
        kMatchTimeoutMilliseconds)]
    internal static partial Regex VerdictLineRegex();

    /// <summary>Matches a standalone verdict word, see <see cref="kVerdictWordPattern"/>.</summary>
    [GeneratedRegex(kVerdictWordPattern, RegexOptions.CultureInvariant, kMatchTimeoutMilliseconds)]
    internal static partial Regex VerdictWordRegex();
}
