using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

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
            string output = RemoveSpecialCharsMand().Replace(text, "").Trim();
            if (output.Length < text.Length)
            {
                shortened = true;
            }
            return output;
        }

        public static string? SanitizeOpt(this string? input)
        {
            bool shortened = false;
            if (input != null)
            {
                string output = SanitizeMand(input, ref shortened);
                return output;
            }
            else return null;
        }

        public static string? SanitizeOpt(this string? input, ref bool shortened)
        {
            if (input != null)
            {
                return SanitizeMand(input, ref shortened);
            }
            else return null;
        }

        // Ldap names: more restrictive due to Ldap restrictions. Chars not allowed (would have to be escaped in Dn):   +;,\"<>#
        public static string SanitizeLdapNameMand(this string input)
        {
            bool shortened = false;
            string output = SanitizeLdapNameMand(input, ref shortened);
            return output;
        }

        // Ldap names: more restrictive due to Ldap restrictions. Chars not allowed (would have to be escaped in Dn):   +;,\"<>#
        public static string SanitizeLdapNameMand(this string input, ref bool shortened)
        {
            string output = RemoveSpecialCharsLdapNameMand().Replace(input, "").Trim();
            if (output.Length < input.Length)
            {
                shortened = true;
            }
            return output;
        }

        public static string? SanitizeLdapNameOpt(this string? input)
        {
            bool shortened = false;
            if (input != null)
            {
                string output = SanitizeLdapNameMand(input, ref shortened);
                return output;
            }
            else return null;
        }

        public static string? SanitizeLdapNameOpt(string? input, ref bool shortened)
        {
            if (input != null)
            {
                return SanitizeLdapNameMand(input, ref shortened);
            }
            else return null;
        }

        // Ldap path (Dn): Additionally needed on top of Ldap names chars:   =,
        public static string SanitizeLdapPathMand(this string input)
        {
            bool shortened = false;
            string output = SanitizeLdapPathMand(input, ref shortened);
            return output;
        }

        // Ldap path (Dn): Additionally needed on top of Ldap names chars:   =,
        public static string SanitizeLdapPathMand(this string input, ref bool shortened)
        {
            string output = RemoveSpecialLdapPathMand().Replace(input, "").Trim();
            if (output.Length < input.Length)
            {
                shortened = true;
            }
            return output;
        }

        public static string? SanitizeLdapPathOpt(this string? input)
        {
            bool shortened = false;
            if (input != null)
            {
                string output = SanitizeLdapPathMand(input, ref shortened);
                return output;
            }
            else return null;
        }

        public static string? SanitizeLdapPathOpt(this string? input, ref bool shortened)
        {
            if (input != null)
            {
                return SanitizeLdapPathMand(input, ref shortened);
            }
            else return null;
        }

        // Passwords should not have Whitespaces except inner blanks
        public static string SanitizePasswMand(this string input)
        {
            bool shortened = false;
            string output = SanitizePasswMand(input, ref shortened);
            return output;
        }

        // Passwords should not have Whitespaces except inner blanks
        public static string SanitizePasswMand(this string input, ref bool shortened)
        {
            string output = RemoveSpecialPasswMand().Replace(input, "").Trim();
            if (output.Length < input.Length)
            {
                shortened = true;
            }
            return output;
        }

        public static string? SanitizePasswOpt(this string? input)
        {
            bool shortened = false;
            if (input != null)
            {
                string output = SanitizePasswMand(input, ref shortened);
                return output;
            }
            else return null;
        }

        public static string? SanitizePasswOpt(this string? input, ref bool shortened)
        {
            if (input != null)
            {
                return SanitizePasswMand(input, ref shortened);
            }
            else return null;
        }

        // Keys are only trimmed
        public static string SanitizeKeyMand(this string input)
        {
            bool shortened = false;
            string output = SanitizeKeyMand(input, ref shortened);
            return output;
        }

        // Keys are only trimmed
        public static string SanitizeKeyMand(this string input, ref bool shortened)
        {
            string output = input.Trim();
            if (output.Length < input.Length)
            {
                shortened = true;
            }
            return output;
        }

        public static string? SanitizeKeyOpt(this string? input)
        {
            bool shortened = false;
            if (input != null)
            {
                string output = SanitizeKeyMand(input, ref shortened);
                return output;
            }
            else return null;
        }

        public static string? SanitizeKeyOpt(this string? input, ref bool shortened)
        {
            if (input != null)
            {
                return SanitizeKeyMand(input, ref shortened);
            }
            else return null;
        }


        // Comments may contain everything but quotes (EOL chars are allowed)
        public static string SanitizeCommentMand(this string input)
        {
            bool shortened = false;
            string output = SanitizeCommentMand(input, ref shortened);
            return output;
        }

        // Comments may contain everything but quotes (EOL chars are allowed)
        public static string SanitizeCommentMand(this string input, ref bool shortened)
        {
            string output = RemoveSpecialCommentMand().Replace(input, "").Trim();
            string ignorableChangeCompareString = output + "\n";

            if (input!=null)
            {
                if (output.Length < input.Length)   // there is always an EOL char added in text fields
                {
                    if (ignorableChangeCompareString != input)
                    {
                        shortened = true;
                    }
                }
            }
            return output;
        }


        public static string? SanitizeCommentOpt(this string? input)
        {
            bool shortened = false;
            if (input != null)
            {
                string output = SanitizeCommentMand(input, ref shortened);
                return output;
            }
            else return null;
        }


        public static string? SanitizeCommentOpt(this string? input, ref bool shortened)
        {
            if (input != null)
            {
                return SanitizeCommentMand(input, ref shortened);
            }
            else return null;
        }

        public static string SanitizeCidrMand(this string input)
        {
            bool shortened = false;
            string output = SanitizeCidrMand(input, ref shortened);
            return output;
        }

        public static string SanitizeCidrMand(this string input, ref bool shortened)
        {
            string output = RemoveSpecialCidrMand().Replace(input, "").Trim();
            if (output.Length < input.Length)
            {
                shortened = true;
            }
            return output;
        }

        public static string? SanitizeCidrOpt(this string? input)
        {
            bool shortened = false;
            if (input != null)
            {
                string output = SanitizeCidrMand(input, ref shortened);
                return output;
            }
            else return null;
        }

        public static string? SanitizeCidrOpt(this string? input, ref bool shortened)
        {
            if (input != null)
            {
                return SanitizeCidrMand(input, ref shortened);
            }
            else return null;
        }

        public static string SanitizeJsonMand(this string input)
        {
            bool shortened = false;
            string output = SanitizeJsonMand(input, ref shortened);
            return output;
        }

        public static string SanitizeJsonMand(this string input, ref bool shortened)
        {
            string output = RemoveSpecialJsonMand().Replace(input, "").Trim();
            if (output.Length < input.Length)
            {
                shortened = true;
            }
            return output;
        }

        // not allowed: +*(){}[]?!#<>=,;'\"'/\\\t@$%^|&~ -> replaced by "_"
        public static string SanitizeJsonFieldMand(this string input)
        {
            bool shortened = false;
            string output = SanitizeJsonFieldMand(input, ref shortened);
            return output;
        }

        public static string SanitizeJsonFieldMand(this string input, ref bool shortened)
        {
            string output = RemoveSpecialJsonFieldMand().Replace(input, "_").Trim();
            if (output.Length < input.Length)
            {
                shortened = true;
            }
            return output;
        }

        public static string SanitizeEolMand(this string input)
        {
            bool shortened = false;
            string output = SanitizeEolMand(input, ref shortened);
            return output;
        }

        public static string SanitizeEolMand(this string input, ref bool shortened)
        {
            string output = RemoveSpecialEolMand().Replace(input, " ").Trim();
            if (output.Length < input.Length)
            {
                shortened = true;
            }
            return output;
        }

        [GeneratedRegex(@"[^\w\.\*\-\:\?@/\(\)\[\]\{\}\$\+<>#\$ ]")]
        private static partial Regex RemoveSpecialCharsMand();

        [GeneratedRegex(@"[^\w\.\*\-\:\?@/\(\) ]")]
        private static partial Regex RemoveSpecialCharsLdapNameMand();

        [GeneratedRegex(@"[^\w\.\*\-\:\?@/\(\)\=\, \\]")]
        private static partial Regex RemoveSpecialLdapPathMand();

        [GeneratedRegex(@"[^\S ]")]
        private static partial Regex RemoveSpecialPasswMand();

        [GeneratedRegex(@"[""'']")]
        private static partial Regex RemoveSpecialCommentMand();

        [GeneratedRegex(@"[^a-fA-F0-9\.\:/]")]
        private static partial Regex RemoveSpecialCidrMand();

        [GeneratedRegex(@"[^\S ]")]
        private static partial Regex RemoveSpecialJsonMand();

        [GeneratedRegex(@"[\+\*\(\)\{\}\[\]\?\!#<>\=\,\;\/\\\t@\$\%\^\|\&\~ ]")]
        private static partial Regex RemoveSpecialJsonFieldMand();

        [GeneratedRegex(@"[\n\r]")]
        private static partial Regex RemoveSpecialEolMand();







        
        
    }
}
