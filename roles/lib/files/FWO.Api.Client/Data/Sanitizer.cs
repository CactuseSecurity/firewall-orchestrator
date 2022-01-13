using System.Text.RegularExpressions;

namespace FWO.Api.Data
{
    public class Sanitizer
    {
        public static string? SanitizeOpt(string? input)
        {
            if (input != null)
            {
                return SanitizeMand(input);
            }
            else return null;
        }

        public static string SanitizeMand(string input)
        {
            return Regex.Replace(input, @"[^\w\.\*\-\:\?@/\(\)]", "").Trim();
        }

        public static string? SanitizePasswOpt(string? input)
        {
            if (input != null)
            {
                return SanitizePasswMand(input);
            }
            else return null;
        }

        public static string SanitizePasswMand(string input)
        {
            return Regex.Replace(input, @"[^\S ]", "").Trim();
        }

        public static string? SanitizeKeyOpt(string? input)
        {
            if (input != null)
            {
                return SanitizeKeyMand(input);
            }
            else return null;
        }

        public static string SanitizeKeyMand(string input)
        {
            return input.Trim();
        }
    }
}
