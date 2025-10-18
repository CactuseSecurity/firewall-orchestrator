using System.Text.RegularExpressions;

namespace FWO.Basics
{
    public static partial class StringExtensions
    {
        public static string SanitizeMand(this string text)
        {
            bool shortened = false;
            string output = SanitizeMand(text, ref shortened); 
            return output;
        }

        public static string SanitizeMand(this string text, ref bool shortened)
        {
            string output = RemoveSpecialChars().Replace(text, "").Trim();
            if (output.Length < text.Length)
            {
                shortened = true;
            }
            return output;
        }

        [GeneratedRegex(@"[^\w\.\*\-\:\?@/\(\)\[\]\{\}\$\+<>#\$\=\, ]")]
        private static partial Regex RemoveSpecialChars();
    }
}
