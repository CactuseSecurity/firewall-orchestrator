namespace FWO.Data.Labelling
{
    /// <summary>An AND-group of labels: its name and the names "key: value" of its labels, sorted.</summary>
    /// <param name="Name">Name of the group, see <see cref="LabelAndGroups"/>.</param>
    /// <param name="LabelNames">The labels of the group, which are ANDed.</param>
    public sealed record LabelAndGroup(string Name, List<string> LabelNames);

    /// <summary>
    /// Names of AND-groups of labels: the names "key: value" of the labels of a group, sorted and joined by " &amp; ", as the
    /// Guardicore import names the group of ANDed labels of a rule side, e.g. "OS: Linux &amp; Stage: Prod". A group of one
    /// label is named like the label. The groups of a side are ORed. The name only identifies a group for comparisons; its
    /// labels are always passed on as a list, as a label value may itself contain " &amp; ".
    /// </summary>
    public static class LabelAndGroups
    {
        /// <summary>Separator of the labels in the name of an AND-group.</summary>
        public const string kAndSeparator = " & ";

        /// <summary>The AND-groups of the labels of a connection side, in the order of their group numbers; a repeated group once.</summary>
        public static List<LabelAndGroup> Groups(IEnumerable<ConnectionLabel> labels)
        {
            ArgumentNullException.ThrowIfNull(labels);
            List<LabelAndGroup> groups = [];
            foreach (IGrouping<int, ConnectionLabel> andGroup in labels.GroupBy(label => label.AndGroup).OrderBy(group => group.Key))
            {
                List<string> labelNames = [.. andGroup.Select(label => label.Display()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
                string name = GroupName(labelNames);
                if (!groups.Exists(group => group.Name == name))
                {
                    groups.Add(new LabelAndGroup(name, labelNames));
                }
            }
            return groups;
        }

        /// <summary>The names of the AND-groups of the labels of a connection side, see <see cref="Groups"/>.</summary>
        public static List<string> GroupNames(IEnumerable<ConnectionLabel> labels) => [.. Groups(labels).Select(group => group.Name)];

        /// <summary>The name of the AND-group of the given label names.</summary>
        public static string GroupName(IEnumerable<string> labelNames) =>
            string.Join(kAndSeparator, labelNames.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
    }
}
