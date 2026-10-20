using FWO.Config.Api;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Logging;

namespace FWO.Services.PathAnalysis
{
    /// <summary>Selects the path analyzer for a requested or the configured algorithm.</summary>
    public sealed class PathAnalyzerFactory(ApiConnection apiConnection, GlobalConfig globalConfig)
    {
        /// <summary>
        /// Creates the analyzer for the given algorithm id, or for the configured algorithm when no id is given.
        /// If an unknown id is found it falls back to the none analyzer and logs a warning.
        /// </summary>
        public IPathAnalyzer Create(long? algorithmId = null)
        {
            long selectedAlgorithmId = algorithmId ?? globalConfig.PathAnalysisAlgorithm;
            switch (selectedAlgorithmId)
            {
                case GlobalConst.kPathAnalysisAlgorithmNone:
                    return new NoPathAnalyzer();
                case GlobalConst.kPathAnalysisAlgorithmNetworkZoneTree:
                    return new NetworkZoneTreePathAnalyzer(apiConnection, globalConfig);
                case GlobalConst.kPathAnalysisAlgorithmForEachDevice:
                    return new DeviceListPathAnalyzer(apiConnection, false);
                case GlobalConst.kPathAnalysisAlgorithmFirstDevice:
                    return new DeviceListPathAnalyzer(apiConnection, true);
                default:
                    Log.WriteWarning("Path Analysis",
                        $"unknown path analysis algorithm id {selectedAlgorithmId}, falling back to none");
                    return new NoPathAnalyzer();
            }
        }
    }
}
