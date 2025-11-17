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

            // Trim + Unicode normalize
            string normalizedInput = input.Trim().Normalize(NormalizationForm.FormC);

            string decoded = HttpUtility.UrlDecode(normalizedInput);
            decoded = HttpUtility.HtmlDecode(decoded);
            if (ScriptTagRegex().IsMatch(decoded) ||
                EventHandlerAttributeRegex().IsMatch(decoded) ||
                JavascriptSchemeRegex().IsMatch(decoded) ||
                DangerousHtmlTagRegex().IsMatch(decoded)
            ) // e.g. onload=, onclick=
            {
                BlockingUrlLog(input);
                return null;
            }
            if (!Uri.TryCreate(decoded, UriKind.Absolute, out var uri))
            {
                BlockingUrlLog(input);
                return null;
            }

            var scheme = uri.Scheme.ToLowerInvariant();
            if (scheme is not ("http" or "https"))
            {
                BlockingUrlLog(input);
                return null;
            }

            if (uri.AbsolutePath.StartsWith(HelpPathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var decodedPathAndQuery = HttpUtility.UrlDecode(uri.PathAndQuery) ?? string.Empty;
                if (!HelpPathWhitelistRegex().IsMatch(decodedPathAndQuery))
                {
                    BlockingUrlLog(input);
                    return null;
                }
            }
            // Sanitize path segments
            var sanitizedPath = string.Join(PathDelimiter, uri.Segments
                .Select(s => HttpUtility.UrlEncode(
                    HttpUtility.UrlDecode(s.TrimEnd('/'))
                ))
            );
            if (!sanitizedPath.StartsWith(PathDelimiter.ToString()))
                sanitizedPath = PathDelimiter + sanitizedPath;

            // Parse and sanitize query parameters (everything after '?')
            var queryParams = HttpUtility.ParseQueryString(uri.Query);
            var sanitizedQuery = new StringBuilder();
            foreach (string key in queryParams)
            {
                if (key == null) continue;

                if (sanitizedQuery.Length > 0)
                    sanitizedQuery.Append('&');

                sanitizedQuery.Append(HttpUtility.UrlEncode(key));
                sanitizedQuery.Append('=');
                var value = queryParams[key]?.Replace("<", "").Replace(">", "");
                sanitizedQuery.Append(HttpUtility.UrlEncode(value));
            }

            // Rebuild URL
            var builder = new UriBuilder(uri)
            {
                Path = sanitizedPath,
                Fragment = string.Empty,
                Query = sanitizedQuery.ToString()
            };

            if (builder.Uri.AbsoluteUri.Length > 2048)
            {
                BlockingUrlLog(input);
                return null;
            }
            return builder.Uri.AbsoluteUri;
        }

        private static void BlockingUrlLog(string url)
        {
            Log.WriteWarning("Sanitizer", $"Blocked unsafe URL: {url.SanitizeMand()}");
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
