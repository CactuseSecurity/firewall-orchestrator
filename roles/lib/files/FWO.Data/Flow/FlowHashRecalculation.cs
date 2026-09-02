namespace FWO.Data.Flow
{
    /// <summary>
    /// Hashes of the flow base objects (network objects, service objects, time objects) by their flow id.
    /// Used to calculate group and access hashes from recalculated member hashes instead of the stored ones.
    /// </summary>
    public class FlowBaseObjectHashes
    {
        public Dictionary<long, string> NwObjects { get; init; } = [];
        public Dictionary<long, string> SvcObjects { get; init; } = [];
        public Dictionary<long, string> TimeObjects { get; init; } = [];
    }

    /// <summary>
    /// Outcome of a flow hash recalculation.
    /// </summary>
    public enum FlowHashRecalculationOutcome
    {
        /// <summary>All stored hashes already match the current hash logic, nothing was written.</summary>
        NoChanges,

        /// <summary>The changed hashes were sent to the flow database, which is not the same as confirming
        /// that they were applied: the mutation result is not evaluated.</summary>
        Updated,

        /// <summary>The recalculated hashes would not be unique, nothing was written.</summary>
        Conflict
    }

    /// <summary>
    /// A single flow entry whose stored hash differs from the hash recalculated with the current hash logic.
    /// </summary>
    public class FlowHashChange
    {
        public long Id { get; init; }
        public string OldHash { get; init; } = "";
        public string NewHash { get; init; } = "";
    }

    /// <summary>
    /// Result of a flow hash recalculation: the entries whose hash changed, per flow entry type, and any
    /// conflicts that prevent the recalculated hashes from being written back (hashes are unique per table).
    /// </summary>
    public class FlowHashRecalculationResult
    {
        public List<FlowHashChange> NwObjects { get; } = [];
        public List<FlowHashChange> NwGroups { get; } = [];
        public List<FlowHashChange> SvcObjects { get; } = [];
        public List<FlowHashChange> SvcGroups { get; } = [];
        public List<FlowHashChange> TimeObjects { get; } = [];
        public List<FlowHashChange> Accesses { get; } = [];
        public List<string> Conflicts { get; } = [];

        public int ChangeCount => NwObjects.Count + NwGroups.Count + SvcObjects.Count + SvcGroups.Count + TimeObjects.Count + Accesses.Count;
        public bool HasChanges => ChangeCount > 0;
        public bool HasConflicts => Conflicts.Count > 0;
    }

    /// <summary>
    /// Recalculates the hashes of all flow entries with the current hash logic.
    /// Technical entries (network objects with an IP range, service objects with ports, time objects with
    /// a time range) get their hash recalculated from their own data. Non-technical entries keep their stored
    /// hash, because it was generated randomly and cannot be reproduced. Groups and accesses are recalculated
    /// from the recalculated hashes of their members.
    /// </summary>
    public static class FlowHashRecalculation
    {
        /// <summary>
        /// Calculates the hash changes needed to bring the given flow data in line with the current hash logic.
        /// </summary>
        /// <param name="flowData">Flow data holding all flow entries with their stored hashes.</param>
        /// <returns>The changed hashes per flow entry type and any uniqueness conflicts found.</returns>
        public static FlowHashRecalculationResult Calculate(FlowSyncFlowData flowData)
        {
            Dictionary<long, string> nwObjectHashes = flowData.NwObjects.Values.ToDictionary(nwObject => nwObject.Id, nwObject => nwObject.TryCalculateHash() ?? nwObject.Hash);
            Dictionary<long, string> svcObjectHashes = flowData.SvcObjects.Values.ToDictionary(svcObject => svcObject.Id, svcObject => svcObject.TryCalculateHash() ?? svcObject.Hash);
            Dictionary<long, string> timeObjectHashes = flowData.TimeObjects.Values.ToDictionary(timeObject => timeObject.Id, timeObject => timeObject.TryCalculateHash() ?? timeObject.Hash);

            FlowBaseObjectHashes baseObjectHashes = new()
            {
                NwObjects = nwObjectHashes,
                SvcObjects = svcObjectHashes,
                TimeObjects = timeObjectHashes
            };

            Dictionary<long, string> nwGroupHashes = flowData.NwGroups.Values.ToDictionary(group => group.Id, group => group.TryCalculateHash(baseObjectHashes) ?? group.Hash);
            Dictionary<long, string> svcGroupHashes = flowData.SvcGroups.Values.ToDictionary(group => group.Id, group => group.TryCalculateHash(baseObjectHashes) ?? group.Hash);
            Dictionary<long, string> accessHashes = flowData.Accesses.Values.ToDictionary(access => access.Id, access => access.TryCalculateHash(baseObjectHashes) ?? access.Hash);

            FlowHashRecalculationResult result = new();
            AddEntryResult(result, result.NwObjects, FlowEntryType.kNwObject, flowData.NwObjects.Values.Select(entry => (entry.Id, entry.Hash)), nwObjectHashes);
            AddEntryResult(result, result.NwGroups, FlowEntryType.kNwGroup, flowData.NwGroups.Values.Select(entry => (entry.Id, entry.Hash)), nwGroupHashes);
            AddEntryResult(result, result.SvcObjects, FlowEntryType.kSvcObject, flowData.SvcObjects.Values.Select(entry => (entry.Id, entry.Hash)), svcObjectHashes);
            AddEntryResult(result, result.SvcGroups, FlowEntryType.kSvcGroup, flowData.SvcGroups.Values.Select(entry => (entry.Id, entry.Hash)), svcGroupHashes);
            AddEntryResult(result, result.TimeObjects, FlowEntryType.kTimeObject, flowData.TimeObjects.Values.Select(entry => (entry.Id, entry.Hash)), timeObjectHashes);
            AddEntryResult(result, result.Accesses, FlowEntryType.kAccess, flowData.Accesses.Values.Select(entry => (entry.Id, entry.Hash)), accessHashes);

            return result;
        }

        /// <summary>
        /// Collects the changed hashes of one flow entry type and reports recalculated hashes that are no longer
        /// unique within that type, as the flow tables require unique hashes.
        /// </summary>
        private static void AddEntryResult(
            FlowHashRecalculationResult result,
            List<FlowHashChange> changes,
            string entryType,
            IEnumerable<(long Id, string OldHash)> entries,
            Dictionary<long, string> newHashes)
        {
            List<(long Id, string OldHash)> entryList = [.. entries];

            changes.AddRange(entryList
                .Where(entry => newHashes[entry.Id] != entry.OldHash)
                .Select(entry => new FlowHashChange { Id = entry.Id, OldHash = entry.OldHash, NewHash = newHashes[entry.Id] }));

            IEnumerable<IGrouping<string, long>> duplicates = entryList
                .Select(entry => entry.Id)
                .GroupBy(id => newHashes[id])
                .Where(hashGroup => hashGroup.Count() > 1);

            foreach (IGrouping<string, long> duplicate in duplicates)
            {
                result.Conflicts.Add($"{entryType} ids {string.Join(", ", duplicate)} would all get hash {duplicate.Key}");
            }
        }
    }
}
