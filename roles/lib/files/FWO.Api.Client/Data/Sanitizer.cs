using System.Text.RegularExpressions;

namespace FWO.Api.Data
{
    public class Sanitizer
    {
        // Standard input fields
        public static string SanitizeMand(string input, ref bool shortened)
        {
            string output = Regex.Replace(input, @"[^\w\.\*\-\:\?@/\(\)\[\]\{\}\$\+<>#\$ ]", "").Trim();
            if(output.Length < input.Length)
            {
                shortened = true;
            }
            return output;
        }

        public static string? SanitizeOpt(string? input, ref bool shortened)
        {
            if (input != null)
            {
                return SanitizeMand(input, ref shortened);
            }
            else return null;
        }


        // Ldap names: more restrictive due to Ldap restrictions. Chars not allowed (would have to be escaped in Dn):   +;,\"<>#
        public static string SanitizeLdapNameMand(string input, ref bool shortened)
        {
            string output = Regex.Replace(input, @"[^\w\.\*\-\:\?@/\(\) ]", "").Trim();
            if(output.Length < input.Length)
            {
                shortened = true;
            }
            return output;
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
        public static string SanitizeLdapPathMand(string input, ref bool shortened)
        {
            string output = Regex.Replace(input, @"[^\w\.\*\-\:\?@/\(\)\=\, \\]", "").Trim();
            if(output.Length < input.Length)
            {
                shortened = true;
            }
            return output;
        }

        public static string? SanitizeLdapPathOpt(string? input, ref bool shortened)
        {
            if (input != null)
            {
                return SanitizeLdapPathMand(input, ref shortened);
            }
            else return null;
        }


        // Passwords should not have Whitespaces except inner blanks
        public static string SanitizePasswMand(string input, ref bool shortened)
        {
            string output = Regex.Replace(input, @"[^\S ]", "").Trim();
            if(output.Length < input.Length)
            {
                shortened = true;
            }
            return output;
        }

        public static string? SanitizePasswOpt(string? input, ref bool shortened)
        {
            if (input != null)
            {
                return SanitizePasswMand(input, ref shortened);
            }
            else return null;
        }


        // Keys are only trimmed
        public static string SanitizeKeyMand(string input, ref bool shortened)
        {
            string output = input.Trim();
            if(output.Length < input.Length)
            {
                shortened = true;
            }
            return output;
        }

        public static string? SanitizeKeyOpt(string? input, ref bool shortened)
        {
            if (input != null)
            {
                return SanitizeKeyMand(input, ref shortened);
            }
            else return null;
        }

        // Comments may contain everything but quotes (EOL chars are allowed)
        public static string? SanitizeCommentOpt(string? input, ref bool shortened)
        {
            if (input!=null)
            {
                return SanitizeCommentMand(input, ref shortened);
            }
            else return null;
        }

        public static string SanitizeCommentMand(string input, ref bool shortened)
        {
            string output = Regex.Replace(input, @"[""'']", "").Trim();
            string ignorableChangeCompareString = output + "\n";
            if (input!=null)
            {
                if(output.Length < input.Length) // there is always an EOL char added in text fields
                {
                    if(ignorableChangeCompareString != input )
                    {
                        shortened = true;
                    }
                }
            }
            return output;
        }

        // Cidrs may contain Numbers[a-f]:./
        public static string SanitizeCidrMand(string input, ref bool shortened)
        {
            string output = Regex.Replace(input, @"[^a-fA-F0-9\.\:/]", "").Trim();
            if(output.Length < input.Length)
            {
                shortened = true;
            }
            return output;
        }

        public static string? SanitizeCidrOpt(string? input, ref bool shortened)
        {
            if (input != null)
            {
                return SanitizeCidrMand(input, ref shortened);
            }
            else return null;
        }

        public static string SanitizeJsonMand(string input, ref bool shortened)
        {
            string output = Regex.Replace(input, @"[^\S ]", "").Trim();
            if(output.Length < input.Length)
            {
                shortened = true;
            }
            return output;
        }

        // not allowed: +*(){}[]?!#<>=,;'\"'/\\\t@$%^|&~
        public static string SanitizeJsonFieldMand(string input, ref bool changed)
        {
            string output = Regex.Replace(input.Trim(), @"[\+\*\(\)\{\}\[\]\?\!#<>\=\,\;\/\\\t@\$\%\^\|\&\~ ]", "_");
            if(output != input)
            {
                changed = true;
            }
            return output;
        }
    }
}
