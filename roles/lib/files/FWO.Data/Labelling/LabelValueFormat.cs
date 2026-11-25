using System.Text;
using System.Text.RegularExpressions;

namespace FWO.Data.Labelling
{
    /// <summary>
    /// Format of the values of the labels FWO maintains for owners (App) and modelled groups (AppRole, AppZone, NetworkArea),
    /// with the placeholders {name} and {id}, e.g. "{name} ({id})" for "FWO-Produktion (AR5003080-005)". The default "{id}"
    /// keeps the bare id. A label is identified by its id, not by its value: <see cref="IdCandidates"/> reads the id back
    /// from a value, so that a label is still found after the name or the format changed.
    /// </summary>
    public sealed class LabelValueFormat
    {
        /// <summary>Placeholder of the name of the owner or modelled group.</summary>
        public const string kNamePlaceholder = "{name}";

        /// <summary>Placeholder of the id of the owner (external app id) or modelled group (id string).</summary>
        public const string kIdPlaceholder = "{id}";

        /// <summary>The default format: the bare id.</summary>
        public const string kDefault = kIdPlaceholder;

        /// <summary>Maximum length of a format.</summary>
        public const int kMaxFormatLength = 100;

        /// <summary>Maximum length of a formatted value; longer names are shortened.</summary>
        public const int kMaxValueLength = 250;

        /// <summary>Text key of the message shown for an invalid format.</summary>
        public const string kInvalidFormatTextKey = "E5603";

        private const string kNameGroup = "name";
        private const string kIdGroup = "id";
        private static readonly TimeSpan kMatchTimeout = TimeSpan.FromMilliseconds(100);

        private readonly Regex pattern;

        /// <summary>The format in effect: the trimmed given format, or the default if it is invalid.</summary>
        public string Format { get; }

        /// <summary>True if the values contain the name, so that a renamed owner or group gets another value.</summary>
        public bool UsesName => Format.Contains(kNamePlaceholder, StringComparison.Ordinal);

        /// <summary>Creates the format; an empty or invalid format falls back to the default.</summary>
        /// <param name="format">Configured format.</param>
        public LabelValueFormat(string? format)
        {
            string trimmed = format?.Trim() ?? "";
            Format = IsValid(trimmed) ? trimmed : kDefault;
            pattern = BuildPattern(Format);
        }

        /// <summary>
        /// True if a rename of an owner or group changes its label value, i.e. the values contain the name and it changed.
        /// </summary>
        /// <param name="previousName">Name before the change.</param>
        /// <param name="name">Name after the change.</param>
        public bool NameChangeAltersValue(string? previousName, string? name)
        {
            return UsesName && !string.Equals(previousName?.Trim() ?? "", name?.Trim() ?? "", StringComparison.Ordinal);
        }

        /// <summary>
        /// True if a format can be used: it contains {id} exactly once, {name} at most once, no other braces and at most
        /// <see cref="kMaxFormatLength"/> characters.
        /// </summary>
        /// <param name="format">Format to check.</param>
        public static bool IsValid(string? format)
        {
            string trimmed = format?.Trim() ?? "";
            if (trimmed.Length == 0 || trimmed.Length > kMaxFormatLength || Occurrences(trimmed, kIdPlaceholder) != 1 || Occurrences(trimmed, kNamePlaceholder) > 1)
            {
                return false;
            }
            string literals = trimmed.Replace(kIdPlaceholder, "", StringComparison.Ordinal).Replace(kNamePlaceholder, "", StringComparison.Ordinal);
            return !literals.Contains('{') && !literals.Contains('}');
        }

        /// <summary>
        /// Returns the label value of an owner or group. Without name the value is the id alone; a name that would make the
        /// value longer than <see cref="kMaxValueLength"/> is shortened.
        /// </summary>
        /// <param name="name">Name of the owner or group.</param>
        /// <param name="id">Id of the owner or group.</param>
        public string Value(string? name, string id)
        {
            string trimmedId = id.Trim();
            string trimmedName = name?.Trim() ?? "";
            if (!UsesName)
            {
                return Format.Replace(kIdPlaceholder, trimmedId, StringComparison.Ordinal);
            }
            if (trimmedName.Length == 0)
            {
                return trimmedId;
            }
            int otherLength = Format.Length - kNamePlaceholder.Length - kIdPlaceholder.Length + trimmedId.Length;
            int maxNameLength = Math.Max(1, kMaxValueLength - otherLength);
            if (trimmedName.Length > maxNameLength)
            {
                trimmedName = trimmedName[..maxNameLength].TrimEnd();
            }
            return Format.Replace(kIdPlaceholder, trimmedId, StringComparison.Ordinal).Replace(kNamePlaceholder, trimmedName, StringComparison.Ordinal);
        }

        /// <summary>
        /// Returns the ids a label value may stand for, most specific first: the id by this format, the text in its last
        /// parentheses (e.g. labels written with another format or by others) and the whole value (e.g. labels of the default
        /// format). Empty candidates are left out.
        /// </summary>
        /// <param name="value">Label value.</param>
        public List<string> IdCandidates(string? value)
        {
            string trimmed = value?.Trim() ?? "";
            List<string> candidates = [];
            AddCandidate(candidates, IdByFormat(trimmed));
            AddCandidate(candidates, IdInLastParentheses(trimmed));
            AddCandidate(candidates, trimmed);
            return candidates;
        }

        /// <summary>True if a label value stands for the given id, see <see cref="IdCandidates"/>.</summary>
        /// <param name="value">Label value.</param>
        /// <param name="id">Id of an owner or group.</param>
        public bool Matches(string? value, string? id)
        {
            string trimmedId = id?.Trim() ?? "";
            return trimmedId.Length > 0 && IdCandidates(value).Contains(trimmedId, StringComparer.Ordinal);
        }

        private string? IdByFormat(string value)
        {
            try
            {
                Match match = pattern.Match(value);
                return match.Success ? match.Groups[kIdGroup].Value.Trim() : null;
            }
            catch (RegexMatchTimeoutException)
            {
                return null;
            }
        }

        private static string? IdInLastParentheses(string value)
        {
            if (!value.EndsWith(')'))
            {
                return null;
            }
            int start = value.LastIndexOf('(');
            return start < 0 ? null : value[(start + 1)..^1].Trim();
        }

        private static void AddCandidate(List<string> candidates, string? candidate)
        {
            if (!string.IsNullOrEmpty(candidate) && !candidates.Contains(candidate, StringComparer.Ordinal))
            {
                candidates.Add(candidate);
            }
        }

        /// <summary>
        /// The pattern of the values of a format: the literal parts verbatim, the name as long as possible and the id as short
        /// as possible, so that for "{name} ({id})" the id is the text in the last parentheses.
        /// </summary>
        private static Regex BuildPattern(string format)
        {
            StringBuilder builder = new("^");
            int position = 0;
            while (position < format.Length)
            {
                if (string.CompareOrdinal(format, position, kIdPlaceholder, 0, kIdPlaceholder.Length) == 0)
                {
                    builder.Append($"(?<{kIdGroup}>.+?)");
                    position += kIdPlaceholder.Length;
                }
                else if (string.CompareOrdinal(format, position, kNamePlaceholder, 0, kNamePlaceholder.Length) == 0)
                {
                    builder.Append($"(?<{kNameGroup}>.*)");
                    position += kNamePlaceholder.Length;
                }
                else
                {
                    builder.Append(Regex.Escape(format[position].ToString()));
                    position++;
                }
            }
            builder.Append('$');
            return new Regex(builder.ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant, kMatchTimeout);
        }

        private static int Occurrences(string text, string placeholder)
        {
            int count = 0;
            int index = text.IndexOf(placeholder, StringComparison.Ordinal);
            while (index >= 0)
            {
                count++;
                index = text.IndexOf(placeholder, index + placeholder.Length, StringComparison.Ordinal);
            }
            return count;
        }
    }
}
