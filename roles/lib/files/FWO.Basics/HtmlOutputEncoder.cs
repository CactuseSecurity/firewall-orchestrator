using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace FWO.Basics
{
    /// <summary>
    /// Encodes values that are written into generated html, for the context they are written into.
    /// Report and notification html is assembled from stored values, and a value is only inert if it is
    /// encoded for the place it lands in: text between tags, the value of a double quoted attribute, and
    /// the target of a link each end differently and each need their own encoding.
    /// </summary>
    public static partial class HtmlOutputEncoder
    {
        /// <summary>
        /// Target a link falls back to when its address may not be used. It stays on the document and
        /// loads nothing.
        /// </summary>
        public const string kBlockedUrlReplacement = "#";

        private const int kUrlPatternTimeoutMs = 100;

        /// <summary>
        /// Matches the scheme a url starts with, after the url has been stripped of the whitespace and
        /// control characters a browser drops before it reads the scheme.
        /// </summary>
        [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9+.\-]*:", RegexOptions.CultureInvariant, kUrlPatternTimeoutMs)]
        private static partial Regex UrlSchemePattern();

        /// <summary>
        /// Encodes a value that is written as text between tags.
        /// </summary>
        /// <param name="value">The value to write, null is treated as empty.</param>
        /// <returns>The value with every character that could start markup encoded.</returns>
        public static string EncodeText(string? value)
        {
            return WebUtility.HtmlEncode(value ?? "");
        }

        /// <summary>
        /// Encodes a value that is written as the value of a double quoted attribute.
        /// Every attribute this code generates is double quoted, and the encoding covers the quote that
        /// would end it as well as the characters that would start a new tag, so the value cannot leave
        /// the attribute it is written into.
        /// </summary>
        /// <param name="value">The value to write, null is treated as empty.</param>
        /// <returns>The value, safe to place between two double quotes.</returns>
        public static string EncodeAttribute(string? value)
        {
            return WebUtility.HtmlEncode(value ?? "");
        }

        /// <summary>
        /// Encodes a link target, refusing any address that does not stay on the generated document.
        /// Reports only ever link to an anchor of their own document or to a page of this application,
        /// so an address naming a scheme or a host is refused rather than encoded: it could otherwise
        /// make the reader, or the headless browser that renders the export, fetch it.
        /// The address is stripped of whitespace and control characters before it is judged, because a
        /// browser drops those before it reads the scheme and would otherwise see a scheme this check
        /// did not.
        /// </summary>
        /// <param name="url">The link target to write, null is treated as blocked.</param>
        /// <returns>The address ready to place in a double quoted attribute, or "#" when it was refused.</returns>
        public static string EncodeLocalUrl(string? url)
        {
            return EncodeAttribute(ValidateLocalUrl(url));
        }

        /// <summary>
        /// Decides whether a link target stays on the generated document, without encoding it.
        /// </summary>
        /// <param name="url">The link target to judge, null is treated as blocked.</param>
        /// <returns>The accepted address, or "#" when it was refused.</returns>
        public static string ValidateLocalUrl(string? url)
        {
            string strippedUrl = StripIgnoredUrlCharacters(url);
            if (strippedUrl.Length == 0)
            {
                return kBlockedUrlReplacement;
            }

            // "//host/path" is a protocol relative url and reaches the network, "\\host" is read the same
            // way by several browsers, so neither may pass as a relative path.
            if (strippedUrl.StartsWith("//", StringComparison.Ordinal)
                || strippedUrl.StartsWith('\\')
                || UrlSchemePattern().IsMatch(strippedUrl))
            {
                return kBlockedUrlReplacement;
            }

            return strippedUrl;
        }

        /// <summary>
        /// Removes the characters a browser ignores while it reads the scheme of a url, so that a value
        /// such as "java\nscript:..." is judged as the scheme the browser will act on.
        /// </summary>
        private static string StripIgnoredUrlCharacters(string? url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return "";
            }

            StringBuilder stripped = new(url.Length);
            foreach (char character in url)
            {
                if (!char.IsControl(character) && !char.IsWhiteSpace(character))
                {
                    stripped.Append(character);
                }
            }

            return stripped.ToString();
        }
    }
}
