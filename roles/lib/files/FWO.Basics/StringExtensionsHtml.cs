using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FWO.Basics
{
    public static partial class StringExtensions
    {

        private const string HtmlTagPattern = "<.*?>";
        private static readonly HashSet<string> SafeHtmlTags = new HashSet<string>(["br", "i", "hr", "b", "u", "strong", "em", "p", "ul", "ol", "li", "span", "div"], StringComparer.Ordinal);


        public static string StripDangerousHtmlTags(this string text, RegexOptions options = RegexOptions.None)
        {
            return Regex.Replace(text, HtmlTagPattern, StripIfDangerousTag, options, TimeSpan.FromMilliseconds(100));
        }

        public static string StripHtmlTags(this string text, RegexOptions options = RegexOptions.None)
        {
            return Regex.Replace(text, HtmlTagPattern, string.Empty, options, TimeSpan.FromMilliseconds(100));
        }

        private static string StripIfDangerousTag(Match tagMatch)
        {
            string tagName = ExtractTagName(tagMatch.Value);
            return SafeHtmlTags.Contains(tagName) ? tagMatch.Value : string.Empty;
        }

        private static string ExtractTagName(string tag)
        {
            if (string.IsNullOrEmpty(tag) || tag.Length < 3)
            {
                return string.Empty;
            }

            int index = 1;
            while (index < tag.Length && (tag[index] == '/' || char.IsWhiteSpace(tag[index])))
            {
                index++;
            }

            int start = index;
            while (index < tag.Length && char.IsLetterOrDigit(tag[index]))
            {
                index++;
            }

            return index > start ? tag[start..index] : string.Empty;
        }
    }
}
