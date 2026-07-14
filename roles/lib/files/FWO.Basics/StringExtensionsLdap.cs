using System.Text.RegularExpressions;

namespace FWO.Basics
{
    public static partial class StringExtensions
    {
        public static string ExtractCommonNameFromDn(this string dn)
        {
            if (string.IsNullOrWhiteSpace(dn))
                return string.Empty;

            var match = MyRegex().Match(dn);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        [GeneratedRegex(@"CN=([^,]+)", RegexOptions.IgnoreCase)]
        private static partial Regex MyRegex();
    }
}
