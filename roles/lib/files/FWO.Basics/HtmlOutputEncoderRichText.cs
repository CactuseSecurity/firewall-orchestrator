using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace FWO.Basics
{
    /// <summary>
    /// Renders user formatted free text, such as a connection reason, as html that can only format.
    /// The text is rebuilt rather than filtered: every tag that is not on the allow list stays visible as
    /// text, allowed tags are written anew without their original attributes, and only a few attributes
    /// whose values are checked against strict patterns (colors and bootstrap color classes) are carried over.
    /// Event handlers, links, images and scripts therefore never reach the page.
    /// </summary>
    public static partial class HtmlOutputEncoder
    {
        private const int kMaxRichTextNestingDepth = 20;
        private const string kRichTextLineBreak = "<br>";
        private const string kStyleAttribute = "style";
        private const string kClassAttribute = "class";
        private const string kColorAttribute = "color";
        private const string kFontTag = "font";

        private static readonly HashSet<string> RichTextContainerTags = new(StringComparer.Ordinal)
        {
            "b", "strong", "i", "em", "u", "s", "mark", "small", "sub", "sup", "p", "ul", "ol", "li", "span", "div", kFontTag
        };

        private static readonly HashSet<string> RichTextVoidTags = new(StringComparer.Ordinal) { "br", "hr" };

        private static readonly HashSet<string> RichTextStyleProperties = new(StringComparer.Ordinal) { "color", "background-color" };

        private static readonly string[] LineBreaks = ["\r\n", "\r", "\n"];

        /// <summary>
        /// Matches a complete start or end tag including its attributes. Text that looks like a tag but does
        /// not match stays text and is encoded.
        /// </summary>
        [GeneratedRegex(@"<(?<close>/)?(?<name>[A-Za-z][A-Za-z0-9]*)(?<attributes>(?:\s+[^\s""'<>/=]+(?:\s*=\s*(?:""[^""]*""|'[^']*'|[^\s""'<>=`]+))?)*)\s*/?>",
            RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)]
        private static partial Regex RichTextTagPattern();

        [GeneratedRegex(@"(?<name>[^\s""'<>/=]+)(?:\s*=\s*(?:""(?<dq>[^""]*)""|'(?<sq>[^']*)'|(?<uq>[^\s""'<>=`]+)))?",
            RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)]
        private static partial Regex RichTextAttributePattern();

        /// <summary>
        /// A css color: hex notation, a color keyword, or rgb()/rgba() with plain numbers. Nothing that could
        /// load a resource or escape the declaration.
        /// </summary>
        [GeneratedRegex(@"^(?:#[0-9A-Fa-f]{3,8}|[A-Za-z]{1,30}|rgba?\(\s*\d{1,3}%?\s*(?:,\s*\d{1,3}%?\s*){2}(?:,\s*(?:0|1|0?\.\d{1,3})\s*)?\))$",
            RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)]
        private static partial Regex RichTextColorPattern();

        [GeneratedRegex(@"^(?:text|bg)-(?:primary|secondary|success|danger|warning|info|light|dark|muted|white|black|body)$",
            RegexOptions.NonBacktracking | RegexOptions.CultureInvariant)]
        private static partial Regex RichTextClassPattern();

        /// <summary>
        /// Renders free text that may carry simple formatting (bold, italic, underline, lists, line breaks,
        /// text and background colors) as html that is safe to place between tags.
        /// Line breaks are kept as &lt;br&gt;, tags left open are closed at the end so the text cannot change
        /// the layout around it.
        /// </summary>
        /// <param name="value">The text to render, null is treated as empty.</param>
        /// <returns>Html that contains only allow-listed formatting tags and encoded text.</returns>
        public static string SanitizeRichText(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            StringBuilder output = new(value.Length);
            List<string> openTags = [];
            int position = 0;
            foreach (Match tag in RichTextTagPattern().Matches(value))
            {
                AppendRichTextPlainText(output, value[position..tag.Index]);
                AppendRichTextTag(output, openTags, tag);
                position = tag.Index + tag.Length;
            }
            AppendRichTextPlainText(output, value[position..]);

            for (int index = openTags.Count - 1; index >= 0; index--)
            {
                output.Append($"</{openTags[index]}>");
            }
            return output.ToString();
        }

        private static void AppendRichTextPlainText(StringBuilder output, string text)
        {
            if (text.Length == 0)
            {
                return;
            }

            // Decoding first keeps entities the author typed (e.g. &amp;) and encodes everything afterwards.
            string encodedText = EncodeText(WebUtility.HtmlDecode(text));
            string[] lines = encodedText.Split(LineBreaks, StringSplitOptions.None);
            output.Append(string.Join(kRichTextLineBreak, lines));
        }

        private static void AppendRichTextTag(StringBuilder output, List<string> openTags, Match tag)
        {
            string tagName = tag.Groups["name"].Value.ToLowerInvariant();
            bool isEndTag = tag.Groups["close"].Success;

            if (RichTextVoidTags.Contains(tagName))
            {
                if (!isEndTag)
                {
                    output.Append($"<{tagName}>");
                }
                return;
            }

            if (!RichTextContainerTags.Contains(tagName))
            {
                AppendRichTextPlainText(output, tag.Value);
                return;
            }

            if (isEndTag)
            {
                CloseRichTextTag(output, openTags, tagName);
            }
            else if (openTags.Count < kMaxRichTextNestingDepth)
            {
                openTags.Add(tagName);
                output.Append($"<{tagName}{BuildRichTextAttributes(tagName, tag.Groups["attributes"].Value)}>");
            }
        }

        /// <summary>
        /// Closes the innermost open tag of the given name together with every tag opened inside it.
        /// An end tag without a matching start tag is dropped.
        /// </summary>
        private static void CloseRichTextTag(StringBuilder output, List<string> openTags, string tagName)
        {
            int openIndex = openTags.LastIndexOf(tagName);
            if (openIndex < 0)
            {
                return;
            }

            for (int index = openTags.Count - 1; index >= openIndex; index--)
            {
                output.Append($"</{openTags[index]}>");
            }
            openTags.RemoveRange(openIndex, openTags.Count - openIndex);
        }

        private static string BuildRichTextAttributes(string tagName, string attributes)
        {
            StringBuilder keptAttributes = new();
            foreach (Match attribute in RichTextAttributePattern().Matches(attributes))
            {
                string attributeName = attribute.Groups["name"].Value.ToLowerInvariant();
                string attributeValue = WebUtility.HtmlDecode(ReadRichTextAttributeValue(attribute));
                string safeValue = attributeName switch
                {
                    kStyleAttribute => FilterRichTextStyle(attributeValue),
                    kClassAttribute => FilterRichTextClasses(attributeValue),
                    kColorAttribute when tagName == kFontTag => FilterRichTextColor(attributeValue),
                    _ => ""
                };
                if (safeValue.Length > 0)
                {
                    keptAttributes.Append($" {attributeName}=\"{EncodeAttribute(safeValue)}\"");
                }
            }
            return keptAttributes.ToString();
        }

        private static string ReadRichTextAttributeValue(Match attribute)
        {
            if (attribute.Groups["dq"].Success)
            {
                return attribute.Groups["dq"].Value;
            }
            if (attribute.Groups["sq"].Success)
            {
                return attribute.Groups["sq"].Value;
            }
            return attribute.Groups["uq"].Value;
        }

        /// <summary>
        /// Keeps only color declarations with a checked color value.
        /// </summary>
        private static string FilterRichTextStyle(string style)
        {
            List<string> keptDeclarations = [];
            foreach (string declaration in style.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int separatorIndex = declaration.IndexOf(':');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                string property = declaration[..separatorIndex].Trim().ToLowerInvariant();
                string color = FilterRichTextColor(declaration[(separatorIndex + 1)..]);
                if (RichTextStyleProperties.Contains(property) && color.Length > 0)
                {
                    keptDeclarations.Add($"{property}: {color}");
                }
            }
            return string.Join("; ", keptDeclarations);
        }

        private static string FilterRichTextClasses(string classes)
        {
            IEnumerable<string> keptClasses = classes
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(className => RichTextClassPattern().IsMatch(className));
            return string.Join(" ", keptClasses);
        }

        private static string FilterRichTextColor(string color)
        {
            string trimmedColor = color.Trim();
            return RichTextColorPattern().IsMatch(trimmedColor) ? trimmedColor : "";
        }
    }
}
