using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace FWO.Ai.Client.ReasonChecks;

/// <summary>
/// Kind of a modelled connection whose reason is checked, see the argument kind of the MCP prompt check_connection_reason.
/// </summary>
public enum ConnectionReasonKind
{
    /// <summary>A regular connection of the app.</summary>
    Connection,

    /// <summary>An interface the app offers to other apps; the reason is the interface description.</summary>
    Interface,

    /// <summary>A connection using an interface of another app.</summary>
    InterfaceUsage
}

/// <summary>
/// Limits of the arguments of the reason check prompts. Keep in step with RequestApplicationPrompts of the MCP server.
/// </summary>
public static class ReasonCheckLimits
{
    /// <summary>Maximum length of the reason to check.</summary>
    public const int kMaxReasonLength = 4000;

    /// <summary>Maximum length of each further argument (name, app, source, destination, service).</summary>
    public const int kMaxContextLength = 10000;

    /// <summary>Maximum length of the language of the answer.</summary>
    public const int kMaxLanguageLength = 32;
}

/// <summary>
/// Check of the functional reason of a connection, interface or interface usage modelled for an app, by the MCP prompt
/// check_connection_reason.
/// </summary>
public sealed class ConnectionReasonCheck
{
    /// <summary>Name of the MCP prompt. Keep in step with RequestApplicationPrompts of the MCP server.</summary>
    public const string kPromptName = "check_connection_reason";

    /// <summary>Argument holding the reason. The argument names are kept in step with the MCP prompt.</summary>
    internal const string kReasonArgument = "reason";

    /// <summary>Argument holding the kind of the connection.</summary>
    internal const string kKindArgument = "kind";

    /// <summary>Argument holding the name of the connection or interface.</summary>
    internal const string kNameArgument = "name";

    /// <summary>Argument holding the name of the app.</summary>
    internal const string kAppArgument = "app";

    /// <summary>Argument holding the sources.</summary>
    internal const string kSourceArgument = "source";

    /// <summary>Argument holding the destinations.</summary>
    internal const string kDestinationArgument = "destination";

    /// <summary>Argument holding the services.</summary>
    internal const string kServiceArgument = "service";

    /// <summary>Argument holding the language of the answer.</summary>
    internal const string kLanguageArgument = "language";

    /// <summary>Value of the kind argument for a regular connection.</summary>
    internal const string kKindConnection = "connection";

    /// <summary>Value of the kind argument for an interface.</summary>
    internal const string kKindInterface = "interface";

    /// <summary>Value of the kind argument for an interface usage.</summary>
    internal const string kKindInterfaceUsage = "interface_usage";

    /// <summary>Marks a context argument that was cut to its maximum length.</summary>
    internal const string kCutMarker = "…";

    /// <summary>All argument names the check may send.</summary>
    internal static readonly IReadOnlyList<string> kArgumentNames =
    [
        kReasonArgument, kKindArgument, kNameArgument, kAppArgument, kSourceArgument, kDestinationArgument,
        kServiceArgument, kLanguageArgument
    ];

    /// <summary>Functional reason of the connection, or the description of an interface.</summary>
    public required string Reason { get; init; }

    /// <summary>Kind of the connection.</summary>
    public ConnectionReasonKind Kind { get; init; } = ConnectionReasonKind.Connection;

    /// <summary>Name of the connection or interface.</summary>
    public string? Name { get; init; }

    /// <summary>Name of the app the connection belongs to.</summary>
    public string? App { get; init; }

    /// <summary>Sources, e.g. app servers, app roles or network areas.</summary>
    public string? Source { get; init; }

    /// <summary>Destinations, e.g. app servers, app roles or network areas.</summary>
    public string? Destination { get; init; }

    /// <summary>Services, e.g. protocols and ports.</summary>
    public string? Service { get; init; }

    /// <summary>Language of the answer, e.g. English or German; the language of the reason if null.</summary>
    public string? Language { get; init; }

    /// <summary>
    /// Creates the prompt request. Arguments that are null or blank are left out, and the context arguments (name, app,
    /// source, destination, service) are cut to <see cref="ReasonCheckLimits.kMaxContextLength"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The reason is blank or longer than <see cref="ReasonCheckLimits.kMaxReasonLength"/>,
    /// or the language is not a plain language name.</exception>
    public AgentPromptRequest ToPromptRequest()
    {
        Dictionary<string, string> arguments = new()
        {
            [kReasonArgument] = ValidateReason(Reason),
            [kKindArgument] = ToKindValue(Kind)
        };
        AddContextArgument(arguments, kNameArgument, Name);
        AddContextArgument(arguments, kAppArgument, App);
        AddContextArgument(arguments, kSourceArgument, Source);
        AddContextArgument(arguments, kDestinationArgument, Destination);
        AddContextArgument(arguments, kServiceArgument, Service);
        string? language = ValidateLanguage(Language);
        if (language != null)
        {
            arguments[kLanguageArgument] = language;
        }
        return new AgentPromptRequest(kPromptName, arguments);
    }

    /// <summary>
    /// Returns the trimmed reason.
    /// </summary>
    /// <exception cref="ArgumentException">The reason is blank or too long.</exception>
    [SuppressMessage("Usage", "CA2208", Justification = "The exception identifies the public Reason property.")]
    private static string ValidateReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("The reason must not be blank.", nameof(Reason));
        }
        string trimmedReason = reason.Trim();
        if (trimmedReason.Length > ReasonCheckLimits.kMaxReasonLength)
        {
            throw new ArgumentException($"The reason must not be longer than {ReasonCheckLimits.kMaxReasonLength} characters.",
                nameof(Reason));
        }
        return trimmedReason;
    }

    /// <summary>
    /// Returns the value of the kind argument of the MCP prompt.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The kind is not defined.</exception>
    private static string ToKindValue(ConnectionReasonKind kind)
    {
        return kind switch
        {
            ConnectionReasonKind.Connection => kKindConnection,
            ConnectionReasonKind.Interface => kKindInterface,
            ConnectionReasonKind.InterfaceUsage => kKindInterfaceUsage,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown kind of connection.")
        };
    }

    /// <summary>
    /// Adds a context argument, trimmed and cut to its maximum length; null or blank values are left out.
    /// </summary>
    private static void AddContextArgument(Dictionary<string, string> arguments, string argumentName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            arguments[argumentName] = CutToContextLength(value.Trim());
        }
    }

    /// <summary>
    /// Cuts a text to <see cref="ReasonCheckLimits.kMaxContextLength"/> characters, ending a cut text with
    /// <see cref="kCutMarker"/>. A surrogate pair is never split.
    /// </summary>
    internal static string CutToContextLength(string text)
    {
        if (text.Length <= ReasonCheckLimits.kMaxContextLength)
        {
            return text;
        }
        int keptLength = ReasonCheckLimits.kMaxContextLength - kCutMarker.Length;
        if (char.IsHighSurrogate(text[keptLength - 1]))
        {
            keptLength--;
        }
        return string.Concat(text.AsSpan(0, keptLength), kCutMarker);
    }

    /// <summary>
    /// Returns the trimmed language, or null if none is given.
    /// </summary>
    /// <exception cref="ArgumentException">The language is not a plain language name or too long.</exception>
    [SuppressMessage("Usage", "CA2208", Justification = "The exception identifies the public Language property.")]
    internal static string? ValidateLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }
        string trimmedLanguage = language.Trim();
        if (trimmedLanguage.Length > ReasonCheckLimits.kMaxLanguageLength || !ReasonCheckPatterns.LanguageRegex().IsMatch(trimmedLanguage))
        {
            throw new ArgumentException($"The language must be a language name of letters, spaces and hyphens of at most {ReasonCheckLimits.kMaxLanguageLength} characters, e.g. English.",
                nameof(Language));
        }
        return trimmedLanguage;
    }
}

/// <summary>
/// Verdict of a reason check, see the answer format of the reason check prompts of the MCP server.
/// </summary>
public enum ReasonVerdict
{
    /// <summary>The answer names no verdict.</summary>
    Unknown,

    /// <summary>GENUINE: all criteria of a genuine justification are met.</summary>
    Genuine,

    /// <summary>INSUFFICIENT: a business purpose is recognisable, but not every criterion is met.</summary>
    Insufficient,

    /// <summary>NOT_GENUINE: no business purpose is recognisable.</summary>
    NotGenuine
}

/// <summary>
/// Reads the verdict from the answer of a reason check prompt.
/// </summary>
public static class ReasonVerdictParser
{
    /// <summary>Start of the verdict word NOT_GENUINE, also written NOT GENUINE.</summary>
    private const string kNotPrefix = "NOT";

    /// <summary>Verdict word INSUFFICIENT.</summary>
    private const string kInsufficient = "INSUFFICIENT";

    /// <summary>
    /// Returns the verdict of the answer: the word of its verdict line ("Verdict: GENUINE"), else the first verdict word in
    /// the answer, else <see cref="ReasonVerdict.Unknown"/>. Works on partial answers while they are streamed.
    /// </summary>
    /// <param name="answer">Answer of the reason check, may be partial or null</param>
    public static ReasonVerdict Parse(string? answer)
    {
        if (string.IsNullOrEmpty(answer))
        {
            return ReasonVerdict.Unknown;
        }
        Match verdictMatch = ReasonCheckPatterns.VerdictLineRegex().Match(answer);
        if (!verdictMatch.Success)
        {
            verdictMatch = ReasonCheckPatterns.VerdictWordRegex().Match(answer);
        }
        return verdictMatch.Success ? ToVerdict(verdictMatch.Groups[ReasonCheckPatterns.kVerdictGroup].Value) : ReasonVerdict.Unknown;
    }

    /// <summary>
    /// Returns the verdict of a matched verdict word.
    /// </summary>
    private static ReasonVerdict ToVerdict(string verdictWord)
    {
        if (verdictWord.StartsWith(kNotPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return ReasonVerdict.NotGenuine;
        }
        return verdictWord.Equals(kInsufficient, StringComparison.OrdinalIgnoreCase) ? ReasonVerdict.Insufficient : ReasonVerdict.Genuine;
    }
}
