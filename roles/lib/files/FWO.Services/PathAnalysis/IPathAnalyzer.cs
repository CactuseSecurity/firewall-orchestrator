using FWO.NetworkTopology;
using NetTools;

namespace FWO.Services.PathAnalysis
{
    public interface IPathAnalyzer
    {
        Task<PathAnalysisResult> AnalyzeAsync(PathAnalysisRequest request);
    }
    /// <summary>Parameters for a path analysis run.</summary>
    public sealed class PathAnalysisRequest
    {
        public List<IPAddressRange> Sources { get; init; } = [];
        public List<IPAddressRange> Destinations { get; init; } = [];
        /// <summary>Matrix used for Network Zone Tree algorithm, ignored by other algorithms.
        /// If omitted for NZT algo, then DesignatedZoneMatrixId is used when set. </summary>
        public long? MatrixId { get; init; }
    }
    public sealed class PathAnalysisResult
    {
        /// <summary>Id of Algorithm in path_analysis_algorithm.</summary>
        public long AlgorithmId { get; init; }
        /// <summary>Deduplicated and unsorted list of devices in path.</summary>
        public List<PathDevice> Devices { get; init; } = [];
        /// <summary>List of input ranges that are not covered by network zones.
        /// Is empty if autoCalculateUndefinedInternalZone is enabled.</summary>
        public List<IPAddressRange> UnresolvedRanges { get; init; } = [];
    }
}
