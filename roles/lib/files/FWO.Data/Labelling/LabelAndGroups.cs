namespace FWO.Data.Labelling
{
    /// <summary>
    /// Names of AND-groups of labels: the names "key: value" of the labels of a group, sorted and joined by " &amp; ", as the
    /// Guardicore import names the group of ANDed labels of a rule side, e.g. "OS: Linux &amp; Stage: Prod". A group of one
    /// label is named like the label. The groups of a side are ORed.
    /// </summary>
    public static class LabelAndGroups
    {
        /// <summary>Separator of the labels in the name of an AND-group.</summary>
        public const string kAndSeparator = " & ";

        /// <summary>The names of the AND-groups of the labels of a connection side, in the order of their group numbers.</summary>
        public static List<string> GroupNames(IEnumerable<ConnectionLabel> labels)
        {
            ArgumentNullException.ThrowIfNull(labels);
            return [.. labels.GroupBy(label => label.AndGroup).OrderBy(group => group.Key)
                .Select(group => GroupName(group.Select(label => label.Display())))
                .Distinct(StringComparer.Ordinal)];
        }

        /// <summary>The name of the AND-group of the given label names.</summary>
        public static string GroupName(IEnumerable<string> labelNames) =>
            string.Join(kAndSeparator, labelNames.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

        /// <summary>The label names of an AND-group name.</summary>
        public static List<string> LabelNames(string groupName)
        {
            ArgumentNullException.ThrowIfNull(groupName);
            return [.. groupName.Split(kAndSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
        }
    }
}
