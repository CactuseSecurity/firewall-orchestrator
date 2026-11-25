using System.Text;
using System.Text.RegularExpressions;

namespace FWO.Data.Labelling
{
    /// <summary>
    /// Format of the values of the labels FWO maintains for owners (App) and modelled groups (AppRole, AppZone, NetworkArea),
    /// with the placeholders {name} and {id}, e.g. "{name} ({id})" for "FWO-Produktion (AR5003080-005)". The default "{id}"
    /// keeps the bare id. A label is identified by its id, not by its value: <see cref="IdCandidates"/> reads the id back
    /// from a value, so that a label is still found after the name or the format changed. Therefore {id} has to be
    /// delimited in the format, see <see cref="IsValid"/>.
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
        /// <see cref="kMaxFormatLength"/> characters. {id} must be delimited from the rest of the value: it may neither be
        /// next to {name} nor next to a character that occurs in ids (letters, digits, '-', '_' and '.'), else the id could
        /// not be read back from a value unambiguously (e.g. "{name}-{id}" for "Prod-AR5003080-005").
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
            return !literals.Contains('{') && !literals.Contains('}') && IsIdDelimited(trimmed);
        }

        /// <summary>Whether {id} is at an end of the format or next to a character that cannot occur in ids, on both sides.</summary>
        private static bool IsIdDelimited(string format)
        {
            int start = format.IndexOf(kIdPlaceholder, StringComparison.Ordinal);
            int end = start + kIdPlaceholder.Length;
            bool delimitedBefore = start == 0
                || (!format[..start].EndsWith(kNamePlaceholder, StringComparison.Ordinal) && !CanOccurInIds(format[start - 1]));
            bool delimitedAfter = end == format.Length
                || (!format[end..].StartsWith(kNamePlaceholder, StringComparison.Ordinal) && !CanOccurInIds(format[end]));
            return delimitedBefore && delimitedAfter;
        }

        private static bool CanOccurInIds(char character) => char.IsLetterOrDigit(character) || character is '-' or '_' or '.';

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
        /// Returns the ids a label value may stand for. A value of this format stands for its id by the format only, so that
        /// a name that itself ends with an id in parentheses (e.g. "AR2 Web (AR1)" for "{id} {name}") names no other group.
        /// Any other value - and every value of the default format "{id}", which matches all of them - stands, most specific
        /// first, for the whole value (e.g. labels of the default format) and the text in its last parentheses (e.g. labels
        /// written with another format or by others). Empty candidates are left out.
        /// </summary>
        /// <param name="value">Label value.</param>
        public List<string> IdCandidates(string? value)
        {
            string trimmed = value?.Trim() ?? "";
            string? idByFormat = IdByFormat(trimmed);
            List<string> candidates = [];
            if (!string.IsNullOrEmpty(idByFormat) && Format != kDefault)
            {
                candidates.Add(idByFormat);
                return candidates;
            }
            AddCandidate(candidates, trimmed);
            AddCandidate(candidates, IdInLastParentheses(trimmed));
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
