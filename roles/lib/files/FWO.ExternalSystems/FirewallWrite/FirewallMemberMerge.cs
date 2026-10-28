namespace FWO.ExternalSystems.FirewallWrite
{
    /// <summary>
    /// Computes the member list of a group for vendor APIs that only replace the whole list: the members currently on the
    /// firewall plus the members to add, minus the members to remove, in their original order.
    /// </summary>
    public static class FirewallMemberMerge
    {
        /// <summary>Merges the members of a group step into the current members.</summary>
        public static List<string> Merge(IEnumerable<string> currentMembers, FirewallWriteStep step)
        {
            ArgumentNullException.ThrowIfNull(currentMembers);
            ArgumentNullException.ThrowIfNull(step);
            HashSet<string> toRemove = new(step.MembersToRemove, StringComparer.OrdinalIgnoreCase);
            List<string> merged = [];
            foreach (string member in currentMembers.Concat(step.MembersToAdd))
            {
                if (!toRemove.Contains(member) && !merged.Contains(member, StringComparer.OrdinalIgnoreCase))
                {
                    merged.Add(member);
                }
            }
            return merged;
        }
    }
}
