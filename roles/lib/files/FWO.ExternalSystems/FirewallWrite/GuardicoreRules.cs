using System.Collections.Immutable;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Reads Guardicore segmentation rules as the rule list returns them: whether a rule belongs to a modelled connection
    /// (its marker in the comment), which protocols it has, and whether it already is as FWO would write it.
    /// </summary>
    public static partial class GuardicoreRules
    {
        private const string kLabels = "labels";
        private const string kSubnets = "subnets";
        private const string kOrLabels = "or_labels";
        private const string kAndLabels = "and_labels";
        private static readonly ImmutableArray<string> SideKeys = ["source", "destination"];
        private static readonly ImmutableArray<string> TextKeys = ["section_position", "action", "ruleset_name", "comments"];

        /// <summary>The id of a rule; empty if it has none.</summary>
        public static string Id(JsonNode? rule) => GuardicoreLabelCriteria.Text(rule?["id"]);

        /// <summary>
        /// A connection marker: the configured marker (e.g. "FWOC", which may contain digits but must not end with one)
        /// followed by the id of the connection (e.g. "FWOC12"), without whitespace.
        /// </summary>
        [GeneratedRegex(@"^\S*[^\d\s]\d+$", RegexOptions.CultureInvariant)]
        private static partial Regex MarkerPattern();

        /// <summary>
        /// Whether a comment names the marker of a connection (e.g. "FWOC12") as a whole word: "FWOC123", "XFWOC12" and
        /// "FWOC12x" name other things. A marker without prefix or id (e.g. a shortened one) is never found, so it cannot
        /// match the markers of other connections.
        /// </summary>
        public static bool HasMarker(string? comment, string marker)
        {
            if (string.IsNullOrEmpty(comment) || !MarkerPattern().IsMatch(marker))
            {
                return false;
            }
            for (int index = comment.IndexOf(marker, StringComparison.Ordinal); index >= 0;
                index = comment.IndexOf(marker, index + 1, StringComparison.Ordinal))
            {
                if (IsWholeWord(comment, index, index + marker.Length))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Reads the id of the connection a comment names by its marker as a whole word (see <see cref="HasMarker"/>): the
        /// digits following the first whole-word occurrence of the configured marker, e.g. "12" for "FWOC12" in "see FWOC12".
        /// The variance analysis reads markers this way for the managements whose rules the writer finds by
        /// <see cref="HasMarker"/>, so both count the same rules as rules of a connection.
        /// </summary>
        /// <param name="comment">Comment of a rule.</param>
        /// <param name="markerPrefix">The configured marker, e.g. "FWOC".</param>
        /// <returns>The id of the connection, null if the comment names none.</returns>
        public static string? FindConnectionId(string? comment, string? markerPrefix)
        {
            if (string.IsNullOrEmpty(comment) || string.IsNullOrEmpty(markerPrefix))
            {
                return null;
            }
            for (int index = comment.IndexOf(markerPrefix, StringComparison.Ordinal); index >= 0;
                index = comment.IndexOf(markerPrefix, index + 1, StringComparison.Ordinal))
            {
                int idStart = index + markerPrefix.Length;
                int idEnd = idStart;
                while (idEnd < comment.Length && char.IsAsciiDigit(comment[idEnd]))
                {
                    idEnd++;
                }
                if (idEnd > idStart && IsWholeWord(comment, index, idEnd))
                {
                    return comment[idStart..idEnd];
                }
            }
            return null;
        }

        /// <summary>Whether the text between start and end is neither preceded nor followed by a letter or digit.</summary>
        private static bool IsWholeWord(string text, int start, int end) =>
            (start == 0 || !char.IsLetterOrDigit(text[start - 1])) && (end == text.Length || !char.IsLetterOrDigit(text[end]));

        /// <summary>Whether a rule lies in the given section and names the marker in its comment.</summary>
        public static bool BelongsTo(JsonNode? rule, string section, string marker) =>
            string.Equals(GuardicoreLabelCriteria.Text(rule?["section_position"]), section, StringComparison.OrdinalIgnoreCase)
            && HasMarker(GuardicoreLabelCriteria.Text(rule?["comments"]), marker);

        /// <summary>Whether a rule has exactly the one given protocol.</summary>
        public static bool HasOnlyProtocol(JsonNode? rule, string protocol)
        {
            List<string> protocols = Texts(rule?["ip_protocols"]);
            return protocols.Count == 1 && string.Equals(protocols[0], protocol, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Whether an existing rule already is as the target rule: same section, action, state, ruleset, comment, protocols,
        /// ports and sides. Labels are compared by their ids, as AND groups in any order; a side with other fields than
        /// labels and subnets (e.g. assets or processes) never matches.
        /// </summary>
        public static bool Matches(JsonNode existing, JsonNode target)
        {
            ArgumentNullException.ThrowIfNull(existing);
            ArgumentNullException.ThrowIfNull(target);
            return Canonical(existing) == Canonical(target);
        }

        private static string Canonical(JsonNode rule)
        {
            JsonObject canonical = [];
            foreach (string key in TextKeys)
            {
                canonical[key] = GuardicoreLabelCriteria.Text(rule[key]).ToUpperInvariant();
            }
            canonical["enabled"] = rule["enabled"] is JsonValue enabled && enabled.TryGetValue(out bool isEnabled) ? isEnabled : true;
            canonical["ip_protocols"] = Sorted(Texts(rule["ip_protocols"]).Select(protocol => protocol.ToUpperInvariant()));
            canonical["ports"] = Sorted(Texts(rule["ports"]));
            canonical["port_ranges"] = Sorted(Items(rule["port_ranges"]).Select(range =>
                $"{GuardicoreLabelCriteria.Text(range?["start"])}-{GuardicoreLabelCriteria.Text(range?["end"])}"));
            foreach (string side in SideKeys)
            {
                canonical[side] = CanonicalSide(rule[side]);
            }
            return canonical.ToJsonString();
        }

        private static JsonObject CanonicalSide(JsonNode? side)
        {
            IEnumerable<string> andGroups = Items(side?[kLabels]?[kOrLabels]).Select(group =>
                string.Join("&", Items(group?[kAndLabels]).Select(LabelId).Order(StringComparer.Ordinal)));
            IEnumerable<string> otherFields = side is JsonObject fields
                ? fields.Where(field => field.Key != kLabels && field.Key != kSubnets && IsFilled(field.Value)).Select(field => field.Key)
                : [];
            return new JsonObject
            {
                [kLabels] = Sorted(andGroups),
                [kSubnets] = Sorted(Texts(side?[kSubnets])),
                ["other"] = Sorted(otherFields)
            };
        }

        /// <summary>The id of a label reference: the id itself, or an object with the id.</summary>
        private static string LabelId(JsonNode? reference) => reference is JsonObject label ? Id(label) : GuardicoreLabelCriteria.Text(reference);

        private static bool IsFilled(JsonNode? value) => value switch
        {
            null => false,
            JsonArray array => array.Count > 0,
            JsonObject obj => obj.Count > 0,
            _ => GuardicoreLabelCriteria.Text(value).Length > 0
        };

        private static List<JsonNode?> Items(JsonNode? node) => node is JsonArray array ? [.. array] : [];

        private static List<string> Texts(JsonNode? node) => [.. Items(node).Select(GuardicoreLabelCriteria.Text)];

        private static JsonArray Sorted(IEnumerable<string> values) =>
            [.. values.Order(StringComparer.Ordinal).Select(value => (JsonNode)JsonValue.Create(value))];
    }
}
