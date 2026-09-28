using FWO.Config.Api;
using FWO.Api.Client;
using FWO.Basics;
using FWO.Logging;

namespace FWO.Services.PathAnalysis
{
    /// <summary>Decides which path analysis to choose from.</summary>
    public class PathAnalyzerFactory(ApiConnection apiConnection, GlobalConfig globalConfig)
    {
        /// <summary>
        /// Reads the selected algorithm id from config. If an unknown id is found the default is the none algorithm.
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
