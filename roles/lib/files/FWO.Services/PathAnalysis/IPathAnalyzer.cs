using FWO.NetworkTopology;
using NetTools;

namespace FWO.Services.PathAnalysis
{
    /// <summary>
    /// Runs a path analysis for a set of source and destination ranges.
    /// The configured algorithm decides which implementation runs.
    /// </summary>
    public interface IPathAnalyzer
    {
        /// <summary>Resolves the firewall devices between every source and every destination of the request.</summary>
        Task<PathAnalysisResult> AnalyzeAsync(PathAnalysisRequest request);
    }
    /// <summary>Parameters for a path analysis run.</summary>
    public sealed class PathAnalysisRequest
    {
        /// <summary>Source ip ranges to resolve.</summary>
        public List<IPAddressRange> Sources { get; init; } = [];
        /// <summary>Destination ip ranges to resolve.</summary>
        public List<IPAddressRange> Destinations { get; init; } = [];
        /// <summary>
        /// Matrix used by the network zone tree algorithm, ignored by other algorithms.
        /// When omitted, the configured designated zone matrix is used.
        /// </summary>
        public int? MatrixId { get; init; }
    }
    /// <summary>The result of a path analysis run.</summary>
    public sealed class PathAnalysisResult
    {
        /// <summary>Id of the algorithm in path_analysis_algorithm.</summary>
        public long AlgorithmId { get; init; }
        /// <summary>
        /// One segment per combination of a source and a destination ip range. Empty for algorithms
        /// that do not resolve zones, such as the routing based one.
        /// </summary>
        public IReadOnlyList<PathSegment> Segments { get; init; } = [];
        /// <summary>
        /// Unordered and deduplicated list of devices in the path.
        /// Derived from Segments where an algorithm provides them,
        /// and the only result of algorithms that do not resolve zones.
        /// </summary>
        public IReadOnlyList<PathDevice> Devices { get; init; } = [];
    }
}
