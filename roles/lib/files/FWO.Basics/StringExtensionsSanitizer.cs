using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FWO.Basics
{
    public static partial class StringExtensions
    {
        private const int kRegexTimeoutMilliseconds = 1000;

        // escapes quotes, backslashes and control characters but keeps non-ASCII text readable in the external ticket
        private static readonly JsonSerializerOptions kJsonStringContentOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

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
            if (input != null && output.Length < input.Length && ignorableChangeCompareString != input)
            {
                shortened = true;
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

        public static string SanitizeEmailAddressMand(this string input, ref bool shortened)
        {
            string output = EmailAddressRegex().Replace(input, "").Trim();
            if (output.Length < input.Length)
            {
                shortened = true;
            }
            return output;
        }

        public static string? SanitizeEmailAddressOpt(this string? input, ref bool shortened)
        {
            if (input != null)
            {
                return input.SanitizeEmailAddressMand(ref shortened);
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

        /// <summary>
        /// Escapes a value for use inside a JSON string literal of a text template, e.g. <c>"text": "@@REASON@@"</c>.
        /// Quotes, backslashes and control characters are escaped, so the value can neither end the string nor add
        /// fields. Line breaks become blanks, as the rendered templates are sent as a single line.
        /// </summary>
        /// <param name="input">The value; null is treated as an empty string.</param>
        /// <returns>The escaped value without surrounding quotes.</returns>
        public static string ToJsonStringContent(this string? input)
        {
            string singleLineInput = EolRegex().Replace(input ?? "", " ");
            string quotedValue = JsonSerializer.Serialize(singleLineInput, kJsonStringContentOptions);
            return quotedValue[1..^1];
        }

        /// <summary>
        /// Replaces a placeholder that stands inside a JSON string literal of a template with the escaped value.
        /// </summary>
        /// <param name="template">The JSON template text.</param>
        /// <param name="placeholder">The placeholder to replace.</param>
        /// <param name="value">The value; null is treated as an empty string.</param>
        /// <returns>The template with the escaped value inserted.</returns>
        public static string ReplaceJsonStringPlaceholder(this string template, string placeholder, string? value)
        {
            return template.Replace(placeholder, value.ToJsonStringContent());
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

        [GeneratedRegex(@"[^\w\.\%\+\-@]", RegexOptions.None, kRegexTimeoutMilliseconds)]
        private static partial Regex EmailAddressRegex();

        [GeneratedRegex(@"[^\S ]")]
        private static partial Regex JsonRegex();

        [GeneratedRegex(@"[\+\*\(\)\{\}\[\]\?\!#<>\=\,\;\/\\\t@\$\%\^\|\&\~ ]")]
        private static partial Regex JsonFieldRegex();

        [GeneratedRegex(@"[\n\r]")]
        private static partial Regex EolRegex();

    }
}
