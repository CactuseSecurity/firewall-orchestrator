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

        private static readonly Dictionary<string, long> kIdsByName = new(StringComparer.OrdinalIgnoreCase)
        {
            [kNone] = GlobalConst.kPathAnalysisAlgorithmNone,
            [kNetworkZoneTree] = GlobalConst.kPathAnalysisAlgorithmNetworkZoneTree
        };

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
        /// Gets the names a caller may request. Excludes <see cref="kNone"/>, which cannot resolve any device.
        /// </summary>
        public static IReadOnlyList<string> Requestable { get; } = [.. kIdsByName.Keys.Where(name => name != kNone)];
    }
}
