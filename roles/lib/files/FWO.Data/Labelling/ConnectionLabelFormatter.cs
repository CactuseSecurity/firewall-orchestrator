namespace FWO.Data.Labelling
{
    /// <summary>
    /// Groups and formats the labels of a connection side: the labels of an AND-group are joined by ∩, the groups by ∪.
    /// </summary>
    public static class ConnectionLabelFormatter
    {
        /// <summary>
        /// Groups the labels of a side by their AND-group, in the order of the group numbers.
        /// </summary>
        public static List<List<ConnectionLabel>> GroupLabels(List<ConnectionLabel> labels)
        {
            return [.. labels.GroupBy(label => label.AndGroup).OrderBy(group => group.Key).Select(group => group.ToList())];
        }

        /// <summary>
        /// Formats AND-groups, e.g. "(Stage: Prod ∩ OS: Linux) ∪ (Stage: Test)", not encoded.
        /// </summary>
        public static string FormatGroups(List<List<ConnectionLabel>> groups)
        {
            return RuleLabelFormatter.FormatSideLabels(ToGroupStructure(groups), RuleLabelFormatter.kHtmlAnd, RuleLabelFormatter.kHtmlOr);
        }

        /// <summary>
        /// Formats the labels of a side, not encoded.
        /// </summary>
        public static string Format(List<ConnectionLabel> labels)
        {
            return FormatGroups(GroupLabels(labels));
        }

        private static List<Dictionary<string, List<string>>> ToGroupStructure(List<List<ConnectionLabel>> groups)
        {
            List<Dictionary<string, List<string>>> result = [];
            foreach (List<ConnectionLabel> group in groups)
            {
                Dictionary<string, List<string>> valuesByKey = [];
                foreach (ConnectionLabel label in group)
                {
                    if (!valuesByKey.TryGetValue(label.Label.Key.Name, out List<string>? values))
                    {
                        values = [];
                        valuesByKey[label.Label.Key.Name] = values;
                    }
                    values.Add(label.Label.Value);
                }
                result.Add(valuesByKey);
            }
            return result;
        }
    }
}
