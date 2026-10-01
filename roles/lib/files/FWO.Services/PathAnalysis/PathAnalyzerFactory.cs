using FWO.Config.Api;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Logging;

namespace FWO.Services.PathAnalysis
{
    /// <summary>Selects the path analyzer for the configured algorithm.</summary>
    public sealed class PathAnalyzerFactory(ApiConnection apiConnection, GlobalConfig globalConfig)
    {
        /// <summary>
        /// Reads the selected algorithm id from config.
        /// If an unknown id is found it falls back to the none analyzer and logs a warning.
        /// </summary>
        public IPathAnalyzer Create()
        {
            switch (globalConfig.PathAnalysisAlgorithm)
            {
                case GlobalConst.kPathAnalysisAlgorithmNone:
                    return new NoPathAnalyzer();
                case GlobalConst.kPathAnalysisAlgorithmNetworkZoneTree:
                    return new NetworkZoneTreePathAnalyzer(apiConnection, globalConfig);
                default:
                    Log.WriteWarning("Path Analysis",
                        $"unknown path analysis algorithm id {globalConfig.PathAnalysisAlgorithm}, falling back to none");
                    return new NoPathAnalyzer();
            }
        }
    }
}
