using FWO.Basics;

namespace FWO.Services.PathAnalysis
{
    /// <summary>Used when no algorithm is selected, and as fallback for an unknown algorithm id.</summary>
    public sealed class NoPathAnalyzer : IPathAnalyzer
    {
        /// <summary>Returns no devices. The returned algorithm id shows that no search was performed.</summary>
        public Task<PathAnalysisResult> AnalyzeAsync(PathAnalysisRequest request)
        {
            return Task.FromResult(new PathAnalysisResult
            {
                AlgorithmId = GlobalConst.kPathAnalysisAlgorithmNone
            });
        }
    }
}
