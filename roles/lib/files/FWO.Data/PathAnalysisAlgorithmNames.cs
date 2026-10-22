using FWO.Basics;

namespace FWO.Data
{
    /// <summary>
    /// Stable API names of the path analysis algorithms and their ids in path_analysis_algorithm.
    /// The names are part of the REST contract; the ids stay internal.
    /// </summary>
    public static class PathAnalysisAlgorithmNames
    {
        /// <summary>
        /// API name of the algorithm that performs no path analysis. Accepted nowhere as a request value.
        /// </summary>
        public const string kNone = "none";
        /// <summary>
        /// API name of the network zone tree algorithm.
        /// </summary>
        public const string kNetworkZoneTree = "networkZoneTree";
        /// <summary>
        /// API name of the device selection taking every gateway, which analyses no path. Accepted nowhere as a request value.
        /// </summary>
        public const string kForEachDevice = "forEachDevice";
        /// <summary>
        /// API name of the device selection taking the first gateway, which analyses no path. Accepted nowhere as a request value.
        /// </summary>
        public const string kFirstDevice = "firstDevice";

        private static readonly Dictionary<string, long> kIdsByName = new(StringComparer.OrdinalIgnoreCase)
        {
            [kNone] = GlobalConst.kPathAnalysisAlgorithmNone,
            [kNetworkZoneTree] = GlobalConst.kPathAnalysisAlgorithmNetworkZoneTree,
            [kForEachDevice] = GlobalConst.kPathAnalysisAlgorithmForEachDevice,
            [kFirstDevice] = GlobalConst.kPathAnalysisAlgorithmFirstDevice
        };

        /// <summary>
        /// Algorithms that do not analyse a path between sources and destinations: none and the device selections used
        /// when the workflow creates implementation tasks.
        /// </summary>
        private static readonly HashSet<long> kIdsWithoutPathAnalysis =
        [
            GlobalConst.kPathAnalysisAlgorithmNone,
            GlobalConst.kPathAnalysisAlgorithmForEachDevice,
            GlobalConst.kPathAnalysisAlgorithmFirstDevice
        ];

        /// <summary>
        /// Gets every valid algorithm name.
        /// </summary>
        public static IReadOnlyCollection<string> All => kIdsByName.Keys;

        /// <summary>
        /// Returns the algorithm id for an API name, compared case-insensitively.
        /// </summary>
        public static bool TryGetId(string name, out long id) => kIdsByName.TryGetValue(name, out id);

        /// <summary>
        /// Returns the API name of an algorithm id, or null for an unknown id.
        /// </summary>
        public static string? GetName(long id) => kIdsByName.FirstOrDefault(pair => pair.Value == id).Key;

        /// <summary>
        /// Returns whether an algorithm analyses the path between sources and destinations. False for none, for the
        /// device selections, which ignore the addresses, and for unknown ids.
        /// </summary>
        public static bool AnalysesPath(long id) => kIdsByName.ContainsValue(id) && !kIdsWithoutPathAnalysis.Contains(id);

        /// <summary>
        /// Gets the names a caller may request: only algorithms that analyse a path.
        /// </summary>
        public static IReadOnlyList<string> Requestable { get; } =
            [.. kIdsByName.Where(pair => AnalysesPath(pair.Value)).Select(pair => pair.Key)];
    }
}
