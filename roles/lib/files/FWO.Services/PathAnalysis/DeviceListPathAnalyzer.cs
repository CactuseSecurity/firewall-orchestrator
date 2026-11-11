using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.NetworkTopology;

namespace FWO.Services.PathAnalysis
{
    /// <summary>
    /// Selects devices without analysing the path: every known gateway (for each device), or only the first one
    /// (first device (if available)). Sources and destinations of the request are ignored.
    /// </summary>
    public sealed class DeviceListPathAnalyzer(ApiConnection apiConnection, bool onlyFirstDevice) : IPathAnalyzer
    {
        /// <summary>
        /// Returns every gateway in the order of the device query, or only the first one. Empty when no gateway exists.
        /// </summary>
        public async Task<PathAnalysisResult> AnalyzeAsync(PathAnalysisRequest request)
        {
            List<Device> devices = await apiConnection.SendQueryAsync<List<Device>>(DeviceQueries.getDeviceDetails) ?? [];
            IEnumerable<Device> selectedDevices = onlyFirstDevice ? devices.Take(1) : devices;
            return new PathAnalysisResult
            {
                AlgorithmId = onlyFirstDevice ? GlobalConst.kPathAnalysisAlgorithmFirstDevice : GlobalConst.kPathAnalysisAlgorithmForEachDevice,
                Devices = [.. selectedDevices.Select(device => new PathDevice { Id = device.Id, Name = device.Name ?? "" })]
            };
        }
    }
}
