using System.Text.RegularExpressions;

namespace FWO.Basics
{
    public static class GlobalExtensions
    {
        private const string HtmlTagPattern = "<.*?>";
        private static readonly string[] AllowedTags = ["br?", "i", "hr"];

        private static string BuildDangerousHtmlTagPattern()
        {
            string allowedTags = string.Join('|', AllowedTags);
            return $"<(?!:{allowedTags}).*?(?<!{allowedTags})>";
        }

        public static string StripHtmlTags(this string text, RegexOptions options = RegexOptions.None)
        {
            return Regex.Replace(text, HtmlTagPattern, string.Empty, options);
        }

        public static string StripDangerousHtmlTags(this string text, RegexOptions options = RegexOptions.None)
        {
            string pattern = BuildDangerousHtmlTagPattern();
            return Regex.Replace(text, pattern, string.Empty, options);
        }
    }
}
