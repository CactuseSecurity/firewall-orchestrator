using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Web;
using FWO.Basics;
using FWO.Logging;
using System.Text.RegularExpressions;

namespace FWO.Ui.Services
{
    public sealed partial class UrlSanitizer : IUrlSanitizer
    {
        private const char PathDelimiter = '/';
        private const string HelpPathPrefix = "/help";


        public string? Clean(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                BlockingUrlLog(input);
                return null;
            }

            string decoded = DecodeInput(input);
            if (ContainsDisallowedPatterns(decoded))
                return BlockAndReturnNull(input);

            if (!TryCreateAbsoluteUri(decoded, out var uri))
                return BlockAndReturnNull(input);

            if (!IsAllowedScheme(uri))
                return BlockAndReturnNull(input);

            if (!IsHelpPathAllowed(uri))
                return BlockAndReturnNull(input);

            var sanitizedPath = SanitizePath(uri);
            var sanitizedQuery = SanitizeQuery(uri);
            var sanitizedUrl = BuildSanitizedUrl(uri, sanitizedPath, sanitizedQuery);

            if (sanitizedUrl.Length > 2048)
                return BlockAndReturnNull(input);

            return sanitizedUrl;
        }

        private static void BlockingUrlLog(string url)
        {
            Log.WriteWarning("Sanitizer", $"Blocked unsafe URL: {url.SanitizeMand()}");
        }

        private static string DecodeInput(string input)
        {
            var normalizedInput = input.Trim().Normalize(NormalizationForm.FormC);
            var decoded = HttpUtility.UrlDecode(normalizedInput);
            return HttpUtility.HtmlDecode(decoded) ?? string.Empty;
        }

        private static bool ContainsDisallowedPatterns(string decoded) =>
            ScriptTagRegex().IsMatch(decoded) ||
            EventHandlerAttributeRegex().IsMatch(decoded) ||
            JavascriptSchemeRegex().IsMatch(decoded) ||
            DangerousHtmlTagRegex().IsMatch(decoded);

        private static bool TryCreateAbsoluteUri(string decoded, [NotNullWhen(true)] out Uri? uri) =>
            Uri.TryCreate(decoded, UriKind.Absolute, out uri);

        private static bool IsAllowedScheme(Uri uri)
        {
            var scheme = uri.Scheme.ToLowerInvariant();
            return scheme is "http" or "https";
        }

        private static bool IsHelpPathAllowed(Uri uri)
        {
            if (!uri.AbsolutePath.StartsWith(HelpPathPrefix, StringComparison.OrdinalIgnoreCase))
                return true;

            var decodedPathAndQuery = HttpUtility.UrlDecode(uri.PathAndQuery) ?? string.Empty;
            return HelpPathWhitelistRegex().IsMatch(decodedPathAndQuery);
        }

        private static string SanitizePath(Uri uri)
        {
            var sanitizedPath = string.Join(PathDelimiter, uri.Segments
                .Select(s => HttpUtility.UrlEncode(
                    HttpUtility.UrlDecode(s.TrimEnd('/'))
                )));
            if (!sanitizedPath.StartsWith(PathDelimiter.ToString()))
            {
                sanitizedPath = PathDelimiter + sanitizedPath;
            }

            return sanitizedPath;
        }

        private static string SanitizeQuery(Uri uri)
        {
            var queryParams = HttpUtility.ParseQueryString(uri.Query);
            var sanitizedQuery = new StringBuilder();

            foreach (string key in queryParams)
            {
                if (key == null)
                    continue;

                if (sanitizedQuery.Length > 0)
                    sanitizedQuery.Append('&');

                sanitizedQuery.Append(HttpUtility.UrlEncode(key));
                sanitizedQuery.Append('=');

                var value = queryParams[key]?.Replace("<", "").Replace(">", "");
                sanitizedQuery.Append(HttpUtility.UrlEncode(value));
            }

            return sanitizedQuery.ToString();
        }

        private static string BuildSanitizedUrl(Uri uri, string sanitizedPath, string sanitizedQuery)
        {
            var builder = new UriBuilder(uri)
            {
                Path = sanitizedPath,
                Fragment = string.Empty,
                Query = sanitizedQuery
            };
            return builder.Uri.AbsoluteUri;
        }

        private static string? BlockAndReturnNull(string rawInput)
        {
            BlockingUrlLog(rawInput);
            return null;
        }

        [GeneratedRegex(@"<\s*script\b", RegexOptions.IgnoreCase, "en-US")]
        private static partial Regex ScriptTagRegex();

        [GeneratedRegex(@"on\w+\s*=", RegexOptions.IgnoreCase, "en-US")]
        private static partial Regex EventHandlerAttributeRegex();

        [GeneratedRegex(@"javascript\s*:", RegexOptions.IgnoreCase, "en-US")]
        private static partial Regex JavascriptSchemeRegex();

        [GeneratedRegex(@"<\s*/?\s*(a|img|iframe|svg|object|embed|link|meta|style|body|html|form|input|video|audio)\b", RegexOptions.IgnoreCase, "en-US")]
        private static partial Regex DangerousHtmlTagRegex();

        [GeneratedRegex(@"^[a-zA-Z0-9/_\-\.\?\&=,:;]*$", RegexOptions.IgnoreCase, "en-US")]
        private static partial Regex HelpPathWhitelistRegex();
    }

}
