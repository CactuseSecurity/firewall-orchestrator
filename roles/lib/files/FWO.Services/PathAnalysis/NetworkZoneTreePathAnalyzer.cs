using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.NetworkTopology;
using FWO.Api.Client.Queries;
using FWO.Data;

namespace FWO.Services.PathAnalysis
{
    /// <summary>
    /// Path analysis algorithm that uses a lowest common ancestor algorithm.
    /// The tables in network_zone need to be filled and model the network as tree-like graph.
    /// Firewall devices are the edges and ip ranges are the vertices.
    /// Each IP range defines its path to an arbitrarily defined root ip range and to the internet.
    /// </summary>
    public sealed class NetworkZoneTreePathAnalyzer(ApiConnection apiConnection, GlobalConfig globalConfig) : IPathAnalyzer
    {
        /// <summary>
        /// Finds devices in the input paths with a lowest common ancestor algorithm.
        /// </summary>
        public async Task<PathAnalysisResult> AnalyzeAsync(PathAnalysisRequest request)
        {
            int matrixId = request.MatrixId ?? globalConfig.DesignatedZoneMatrixId;
            if (matrixId <= 0)
            {
                throw new InvalidOperationException(
                    "No zone matrix was given and no designated zone matrix is configured.");
            }
            MatrixData networkData = await LoadNetworkDataAsync(matrixId);
            NetworkZoneTreeAlgorithm algorithm = new(networkData);
            QueryInput queryInput = new()
            {
                Sources = request.Sources,
                Destinations = request.Destinations
            };
            List<PathSegment> pathSegments = algorithm.FindDevicesInPaths(queryInput);
            return new PathAnalysisResult
            {
                AlgorithmId = GlobalConst.kPathAnalysisAlgorithmNetworkZoneTree,
                Segments = pathSegments,
                Devices = [.. pathSegments.SelectMany(segment => segment.Devices).Distinct()]
            };
        }

        /// <summary>
        /// Loads zones, ip ranges and both device path tables of one matrix in parallel.
        /// </summary>
        private async Task<MatrixData> LoadNetworkDataAsync(int matrixId)
        {
            Task<List<ComplianceNetworkZone>> zonesTask = apiConnection.SendQueryAsync<List<ComplianceNetworkZone>>(
                NetworkZoneQueries.getNetworkZonesForMatrix, new { criterionId = matrixId });
            Task<List<NetworkZoneIpRange>> ipRangesTask = apiConnection.SendQueryAsync<List<NetworkZoneIpRange>>(
                NetworkZoneQueries.getIpRangesForMatrix, new { matrixId });
            Task<List<NetworkZoneDeviceIpRange>> rootPathsTask = apiConnection.SendQueryAsync<List<NetworkZoneDeviceIpRange>>(
                NetworkZoneQueries.getNetworkZoneDeviceIpRangeRoot, new { matrixId });
            Task<List<NetworkZoneDeviceIpRange>> internetPathsTask = apiConnection.SendQueryAsync<List<NetworkZoneDeviceIpRange>>(
                NetworkZoneQueries.getNetworkZoneDeviceIpRangeInternet, new { matrixId });

            await Task.WhenAll(zonesTask, ipRangesTask, rootPathsTask, internetPathsTask);
            return new MatrixData
            {
                Zones = zonesTask.Result ?? [],
                IpRanges = ipRangesTask.Result ?? [],
                RootPaths = rootPathsTask.Result ?? [],
                InternetPaths = internetPathsTask.Result ?? []
            };
        }
    }
}
