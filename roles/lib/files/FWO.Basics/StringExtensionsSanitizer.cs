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
            string output = StandardSanitizationRegex().Replace(text, "").Trim();
            if (output.Length < text.Length)
            {
                shortened = true;
            }
            return output;
        }

        public static string? SanitizeOpt(this string? text, ref bool shortened)
        {
            if (text != null)
            {
                return text.SanitizeMand(ref shortened);
            }
            else return null;
        }

        public static string SanitizeLdapNameMand(this string input, ref bool shortened)
        {
            string output = LdapNameRegex().Replace(input, "").Trim();
            if (output.Length < input.Length)
            {
                shortened = true;
            }
            return output;
        }

        public static string? SanitizeLdapNameOpt(this string? input, ref bool shortened)
        {
            if (input != null)
            {
                return input.SanitizeLdapNameMand(ref shortened);
            }
            else return null;
        }

        public static string SanitizeLdapPathMand(this string input, ref bool shortened)
        {
            string output = LdapPathRegex().Replace(input, "").Trim();
            if (output.Length < input.Length)
            {
                shortened = true;
            }
            return output;
        }

        public static string? SanitizeLdapPathOpt(this string? input, ref bool shortened)
        {
            if (input != null)
            {
                return input.SanitizeLdapPathMand(ref shortened);
            }
            else return null;
        }

        public static string SanitizePasswMand(this string input, ref bool shortened)
        {
            string output = PasswdRegex().Replace(input, "").Trim();
            if (output.Length < input.Length)
            {
                shortened = true;
            }
            return output;
        }

        public static string? SanitizePasswOpt(this string? input, ref bool shortened)
        {
            if (input != null)
            {
                return input.SanitizePasswMand(ref shortened);
            }
            else return null;
        }

        public static string SanitizeKeyMand(this string input, ref bool shortened)
        {
            string output = input.Trim();
            if (output.Length < input.Length)
            {
                shortened = true;
            }
            return output;
        }

        public static string? SanitizeKeyOpt(this string? input, ref bool shortened)
        {
            if (input != null)
            {
                return input.SanitizeKeyMand(ref shortened);
            }
            else return null;
        }

        public static string SanitizeCommentMand(this string input, ref bool shortened)
        {
            string output = CommentRegex().Replace(input, "").Trim();
            string ignorableChangeCompareString = output + "\n";
            if (input != null)
            {
                if (output.Length < input.Length) // there is always an EOL char added in text fields
                {
                    if (ignorableChangeCompareString != input)
                    {
                        shortened = true;
                    }
                }
            }
            return output;
        }

        public static string? SanitizeCommentOpt(this string? input, ref bool shortened)
        {
            if (input != null)
            {
                return input.SanitizeCommentMand(ref shortened);
            }
            else return null;
        }

        public static string SanitizeCidrMand(this string input, ref bool shortened)
        {
            string output = CidrRegex().Replace(input, "").Trim();
            if (output.Length < input.Length)
            {
                shortened = true;
            }
            return output;
        }

        public static string? SanitizeCidrOpt(this string? input, ref bool shortened)
        {
            if (input != null)
            {
                return input.SanitizeCidrMand(ref shortened);
            }
            else return null;
        }

        public static string SanitizeJsonMand(this string input, ref bool shortened)
        {
            string output = JsonRegex().Replace(input, "").Trim();
            if (output.Length < input.Length)
            {
                shortened = true;
            }
            return output;
        }

        public static string SanitizeJsonFieldMand(this string input, ref bool changed)
        {
            string output = JsonFieldRegex().Replace(input.Trim(), "_");
            if (output != input)
            {
                changed = true;
            }
            return output;
        }

        public static string SanitizeEolMand(this string input, ref bool shortened)
        {
            string output = EolRegex().Replace(input, " ").Trim();
            if (output.Length < input.Length)
            {
                shortened = true;
            }
            return output;
        }

        [GeneratedRegex(@"[^\w\.\*\-\:\?@/\(\)\[\]\{\}\$\+<>#\$ ]")]
        private static partial Regex StandardSanitizationRegex();

        [GeneratedRegex(@"[^\w\.\*\-\:\?@/\(\) ]")]
        private static partial Regex LdapNameRegex();

        [GeneratedRegex(@"[^\w\.\*\-\:\?@/\(\)\=\, \\]")]
        private static partial Regex LdapPathRegex();

        [GeneratedRegex(@"[^\S ]")]
        private static partial Regex PasswdRegex();

        [GeneratedRegex(@"[""'']")]
        private static partial Regex CommentRegex();

        [GeneratedRegex(@"[^a-fA-F0-9\.\:/]")]
        private static partial Regex CidrRegex();

        [GeneratedRegex(@"[^\S ]")]
        private static partial Regex JsonRegex();

        [GeneratedRegex(@"[\+\*\(\)\{\}\[\]\?\!#<>\=\,\;\/\\\t@\$\%\^\|\&\~ ]")]
        private static partial Regex JsonFieldRegex();

        [GeneratedRegex(@"[\n\r]")]
        private static partial Regex EolRegex();

    }
}
