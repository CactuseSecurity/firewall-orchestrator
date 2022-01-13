using System.Text.RegularExpressions;

namespace FWO.Api.Data
{
    public class Sanitizer
    {
        // Standard input fields
        public static string SanitizeMand(string input)
        {
            return Regex.Replace(input, @"[^\w\.\*\-\:\?@/\(\)\[\]\{\}\$\+<>#\$]", "").Trim();
        }

        public static string? SanitizeOpt(string? input)
        {
            if (input != null)
            {
                return SanitizeMand(input);
            }
            else return null;
        }


        // Ldap names: more restrictive due to Ldap restrictions. Chars not allowed (would have to be escaped in Dn):   +;,\"<>#
        public static string SanitizeLdapNameMand(string input)
        {
            return Regex.Replace(input, @"[^\w\.\*\-\:\?@/\(\)]", "").Trim();
        }

        public static string? SanitizeLdapNameOpt(string? input)
        {
            if (input != null)
            {
                return SanitizeLdapNameMand(input);
            }
            else return null;
        }


        // Ldap path (Dn): Additionally needed on top of Ldap names chars:   =,
        public static string SanitizeLdapPathMand(string input)
        {
            return Regex.Replace(input, @"[^\w\.\*\-\:\?@/\(\)=,]", "").Trim();
        }

        public static string? SanitizeLdapPathOpt(string? input)
        {
            if (input != null)
            {
                return SanitizeLdapPathMand(input);
            }
            else return null;
        }


        // Passwords should not have Whitespaces except inner blanks
        public static string SanitizePasswMand(string input)
        {
            return Regex.Replace(input, @"[^\S ]", "").Trim();
        }

        public static string? SanitizePasswOpt(string? input)
        {
            if (input != null)
            {
                return SanitizePasswMand(input);
            }
            else return null;
        }


        // Keys are only trimmed
        public static string SanitizeKeyMand(string input)
        {
            return input.Trim();
        }

        public static string? SanitizeKeyOpt(string? input)
        {
            if (input != null)
            {
                return SanitizeKeyMand(input);
            }
            else return null;
        }
    }
}
