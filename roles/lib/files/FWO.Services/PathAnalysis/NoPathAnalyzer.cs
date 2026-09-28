using FWO.Basics;

namespace FWO.Services.PathAnalysis
{
    /// <summary>Default when no path analysis algorithm is selected.</summary>
    public class NoPathAnalyzer : IPathAnalyzer
    {
        /// <summary>Returns no devices.</summary>
        public Task<PathAnalysisResult> AnalyzeAsync(PathAnalysisRequest request)
        {
            return Task.FromResult(new PathAnalysisResult
            {
                AlgorithmId = GlobalConst.kPathAnalysisAlgorithmNone
            });
        }
    }
}