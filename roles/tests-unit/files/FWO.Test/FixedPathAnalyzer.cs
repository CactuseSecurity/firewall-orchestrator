using FWO.NetworkTopology;
using FWO.Services.PathAnalysis;

namespace FWO.Test
{
    /// <summary>
    /// Path analyzer returning fixed gateways, or failing, and recording the requests it got.
    /// </summary>
    internal sealed class FixedPathAnalyzer(List<int> deviceIds, bool fail = false) : IPathAnalyzer
    {
        public List<PathAnalysisRequest> Requests { get; } = [];

        public Task<PathAnalysisResult> AnalyzeAsync(PathAnalysisRequest request)
        {
            Requests.Add(request);
            if (fail)
            {
                throw new InvalidOperationException("Simulated path analysis failure.");
            }
            return Task.FromResult(new PathAnalysisResult
            {
                Devices = [.. deviceIds.Select(id => new PathDevice { Id = id, Name = $"gw-{id}" })]
            });
        }
    }
}
