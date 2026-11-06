using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FWO.Data.Labelling
{
    /// <summary>
    /// Formats the labels of rules for reports. The labels of a rule side are ORed AND-groups:
    /// the labels within a group are joined by the AND symbol, the groups by the OR symbol.
    /// </summary>
    public static class RuleLabelFormatter
    {
        /// <summary>
        /// AND symbol for html, pdf and the UI.
        /// </summary>
        public const string kHtmlAnd = " ∩ ";

        /// <summary>
        /// OR symbol for html, pdf and the UI.
        /// </summary>
        public const string kHtmlOr = " ∪ ";

        /// <summary>
        /// AND keyword for csv.
        /// </summary>
        public const string kCsvAnd = " AND ";

        /// <summary>
        /// OR keyword for csv.
        /// </summary>
        public const string kCsvOr = " OR ";

        private const string kLabelSeparator = ", ";
        private static readonly List<Dictionary<string, List<string>>> kNoGroups = [];
        private static readonly List<RuleLabelAssignment> kNoRuleLabels = [];

        /// <summary>
        /// Formats the labels of a rule side, e.g. "(AppRole: AR1 ∩ Stage: Prod) ∪ (AppRole: AR2)".
        /// A single group is not put in brackets. Returns an empty string without labels.
        /// </summary>
        /// <param name="groups">The ORed AND-groups of the side.</param>
        /// <param name="andSeparator">Separator of the labels within a group.</param>
        /// <param name="orSeparator">Separator of the groups.</param>
        /// <returns>The formatted labels, not encoded.</returns>
        public static string FormatSideLabels(List<Dictionary<string, List<string>>>? groups, string andSeparator, string orSeparator)
        {
            List<string> formattedGroups = [.. (groups ?? kNoGroups)
                .Select(group => string.Join(andSeparator, FormatLabels(group)))
                .Where(formattedGroup => formattedGroup.Length > 0)];
            if (formattedGroups.Count <= 1)
            {
                return formattedGroups.FirstOrDefault() ?? "";
            }
            return string.Join(orSeparator, formattedGroups.Select(formattedGroup => $"({formattedGroup})"));
        }

        /// <summary>
        /// Formats the labels of a rule as a whole, e.g. "App: APP-1, App: APP-2". Returns an empty string without labels.
        /// </summary>
        /// <param name="labels">The rule-level labels.</param>
        /// <returns>The formatted labels, not encoded.</returns>
        public static string FormatRuleLabels(List<RuleLabelAssignment>? labels)
        {
            return string.Join(kLabelSeparator, FormatLabels(GroupRuleLabels(labels)));
        }

        /// <summary>
        /// Returns the labels of a rule side in the group structure they are stored in, as JSON:
        /// [{"key": "value" | ["value", ...], ...}, ...], or null without labels.
        /// </summary>
        /// <param name="groups">The ORed AND-groups of the side.</param>
        /// <returns>The JSON text.</returns>
        public static string SideLabelsToJson(List<Dictionary<string, List<string>>>? groups)
        {
            return JsonConvert.SerializeObject(groups is { Count: > 0 } ? groups : null, Formatting.None, new RuleLabelsJsonConverter());
        }

        /// <summary>
        /// Returns the rule-level labels as JSON object {"key": "value" | ["value", ...], ...}.
        /// </summary>
        /// <param name="labels">The rule-level labels.</param>
        /// <returns>The JSON text.</returns>
        public static string RuleLabelsToJson(List<RuleLabelAssignment>? labels)
        {
            JObject result = [];
            foreach ((string key, List<string> values) in GroupRuleLabels(labels))
            {
                result[key] = values.Count == 1 ? new JValue(values[0]) : new JArray(values);
            }
            return result.ToString(Formatting.None);
        }

        private static Dictionary<string, List<string>> GroupRuleLabels(List<RuleLabelAssignment>? labels)
        {
            Dictionary<string, List<string>> valuesByKey = [];
            foreach (LabelValue label in (labels ?? kNoRuleLabels).Select(assignment => assignment.Label))
            {
                if (!valuesByKey.TryGetValue(label.Key.Name, out List<string>? values))
                {
                    values = [];
                    valuesByKey[label.Key.Name] = values;
                }
                if (!values.Contains(label.Value))
                {
                    values.Add(label.Value);
                }
            }
            return valuesByKey;
        }

        private static IEnumerable<string> FormatLabels(Dictionary<string, List<string>> valuesByKey)
        {
            return valuesByKey.SelectMany(label => label.Value.Select(value => $"{label.Key}: {value}"));
        }
    }
}
