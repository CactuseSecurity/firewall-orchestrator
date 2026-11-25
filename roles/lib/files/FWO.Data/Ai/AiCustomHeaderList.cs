using System.Text.Json;

namespace FWO.Data.Ai
{
    /// <summary>
    /// Additional HTTP headers the AI harness sends to the AI provider (<see cref="AiSettings.CustomHeaders"/>), stored in
    /// the global config as JSON array of objects with name and value, e.g.
    /// <c>[{"name":"x-gateway-profile","value":"my-profile"}]</c>. The global config is readable by every role, so
    /// the headers must not carry secrets; the headers that carry the credentials of the provider and the headers that
    /// frame the HTTP message are refused.
    /// </summary>
    public static class AiCustomHeaderList
    {
        /// <summary>Stored value without additional headers.</summary>
        public const string kNone = "[]";

        /// <summary>Maximum number of additional headers.</summary>
        public const int kMaxHeaders = 20;

        /// <summary>Maximum length of the name of a header.</summary>
        public const int kMaxNameLength = 128;

        /// <summary>Maximum length of the value of a header.</summary>
        public const int kMaxValueLength = 2048;

        /// <summary>Characters besides ASCII letters and digits that a header name (a token of RFC 9110) may contain.</summary>
        private const string kTokenSpecialChars = "!#$%&'*+-.^_`|~";

        private const char kFirstVisibleChar = ' ';
        private const char kLastVisibleChar = '~';
        private const char kHorizontalTab = '\t';

        /// <summary>
        /// Headers that cannot be set: those carrying the credentials of the providers, which belong into the credential
        /// store, and those the HTTP client sets itself to frame the message.
        /// </summary>
        private static readonly HashSet<string> kReservedNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Authorization", "Proxy-Authorization", "Cookie", "x-api-key", "api-key", "x-goog-api-key",
            "Host", "Content-Type", "Content-Length", "Content-Encoding", "Transfer-Encoding", "Connection", "Keep-Alive",
            "Proxy-Connection", "TE", "Trailer", "Upgrade", "Expect"
        };

        /// <summary>
        /// Reads stored headers, normalized like <see cref="Normalize"/> does. A blank text means no headers, as the
        /// setting was not stored yet.
        /// </summary>
        /// <param name="text">Stored value, a JSON array of objects with name and value</param>
        /// <param name="headers">The headers; empty if the value cannot be read</param>
        /// <returns>False if the value is not a JSON array of headers.</returns>
        public static bool TryParse(string? text, out List<AiCustomHeader> headers)
        {
            headers = [];
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }
            try
            {
                List<AiCustomHeader?>? storedHeaders = JsonSerializer.Deserialize<List<AiCustomHeader?>>(text);
                if (storedHeaders == null)
                {
                    return false;
                }
                headers = Normalize(storedHeaders);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        /// <summary>
        /// Returns the value to store for the given headers, normalized like <see cref="Normalize"/> does.
        /// </summary>
        /// <param name="headers">Headers to store</param>
        public static string Serialize(IEnumerable<AiCustomHeader?> headers)
        {
            ArgumentNullException.ThrowIfNull(headers);
            return JsonSerializer.Serialize(Normalize(headers));
        }

        /// <summary>
        /// Returns copies of the headers with trimmed name and value, in their order, without headers whose name and value
        /// are both blank, e.g. a row of the settings page that was added but not filled.
        /// </summary>
        /// <param name="headers">Headers as entered or stored</param>
        public static List<AiCustomHeader> Normalize(IEnumerable<AiCustomHeader?> headers)
        {
            ArgumentNullException.ThrowIfNull(headers);
            IEnumerable<AiCustomHeader> trimmedHeaders = headers
                .OfType<AiCustomHeader>()
                .Select(header => new AiCustomHeader { Name = (header.Name ?? "").Trim(), Value = (header.Value ?? "").Trim() });
            return [.. trimmedHeaders.Where(header => header.Name.Length > 0 || header.Value.Length > 0)];
        }

        /// <summary>
        /// Checks the headers as given, without normalizing them: at most <see cref="kMaxHeaders"/>, each with a valid
        /// name that is not reserved and occurs once (ignoring case), and with a valid value.
        /// </summary>
        /// <param name="headers">Headers to check</param>
        /// <returns>The problems found, each once; empty if the headers are valid.</returns>
        public static List<AiSettingsProblem> FindProblems(IReadOnlyCollection<AiCustomHeader> headers)
        {
            ArgumentNullException.ThrowIfNull(headers);
            List<AiSettingsProblem> problems = [];
            if (headers.Count > kMaxHeaders)
            {
                problems.Add(AiSettingsProblem.CustomHeadersTooMany);
            }
            if (headers.Any(header => !IsValidName(header.Name)))
            {
                problems.Add(AiSettingsProblem.CustomHeaderNameInvalid);
            }
            if (headers.Any(header => IsReservedName(header.Name)))
            {
                problems.Add(AiSettingsProblem.CustomHeaderNameReserved);
            }
            if (HasDuplicateNames(headers))
            {
                problems.Add(AiSettingsProblem.CustomHeaderNameDuplicate);
            }
            if (headers.Any(header => !IsValidValue(header.Value)))
            {
                problems.Add(AiSettingsProblem.CustomHeaderValueInvalid);
            }
            return problems;
        }

        /// <summary>
        /// Tells whether a header name is a token of RFC 9110 (ASCII letters, digits and <see cref="kTokenSpecialChars"/>)
        /// of at most <see cref="kMaxNameLength"/> characters.
        /// </summary>
        /// <param name="name">Header name</param>
        public static bool IsValidName(string? name)
        {
            return !string.IsNullOrEmpty(name) && name.Length <= kMaxNameLength && name.All(IsTokenChar);
        }

        /// <summary>
        /// Tells whether a header cannot be set, as it carries credentials or frames the HTTP message.
        /// </summary>
        /// <param name="name">Header name</param>
        public static bool IsReservedName(string? name)
        {
            return name != null && kReservedNames.Contains(name.Trim());
        }

        /// <summary>
        /// Tells whether a header value is not blank, has at most <see cref="kMaxValueLength"/> characters, and consists
        /// of visible ASCII characters, spaces and tabs only, so that it cannot end the header line.
        /// </summary>
        /// <param name="value">Header value</param>
        public static bool IsValidValue(string? value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Length <= kMaxValueLength && value.All(IsValueChar);
        }

        /// <summary>
        /// Tells whether a name occurs more than once, ignoring case.
        /// </summary>
        private static bool HasDuplicateNames(IReadOnlyCollection<AiCustomHeader> headers)
        {
            HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
            return headers.Any(header => !names.Add(header.Name ?? ""));
        }

        /// <summary>
        /// Tells whether a character may be part of a token of RFC 9110.
        /// </summary>
        private static bool IsTokenChar(char character)
        {
            return char.IsAsciiLetterOrDigit(character) || kTokenSpecialChars.Contains(character);
        }

        /// <summary>
        /// Tells whether a character may be part of a header value: visible ASCII, space or tab.
        /// </summary>
        private static bool IsValueChar(char character)
        {
            return character == kHorizontalTab || (character >= kFirstVisibleChar && character <= kLastVisibleChar);
        }
    }
}
