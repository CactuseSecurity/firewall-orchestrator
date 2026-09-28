using FWO.Api.Client;
using FWO.Basics;
using FWO.Config.Api;
using FWO.NetworkTopology;
using FWO.Api.Client.Queries;
using FWO.Data;

namespace FWO.Services.PathAnalysis
{
    public class NetworkZoneTreePathAnalyzer(ApiConnection apiConnection, GlobalConfig globalConfig) : IPathAnalyzer
    {
        
        public async Task<PathAnalysisResult> AnalyzeAsync(PathAnalysisRequest request)
        {
            long matrixId = request.MatrixId ?? globalConfig.DesignatedZoneMatrixId;
            //todo: if matrixId==0 warning in ui
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

        private async Task<MatrixData> LoadNetworkDataAsync(long matrixId)
        {
            Task<List<ComplianceNetworkZone>> zonesTask = apiConnection.SendQueryAsync<List<ComplianceNetworkZone>>(
                NetworkZoneQueries.getNetworkZonesForMatrix, new { criterionId = matrixId });
            Task<List<NetworkZoneIpRange>> ipRangesTask = apiConnection.SendQueryAsync<List<NetworkZoneIpRange>>(
                NetworkZoneQueries.getIpRangesForMatrix, new { matrixId });
            Task<List<NetworkZoneDeviceIpRange>> rootPathsTask  = apiConnection.SendQueryAsync<List<NetworkZoneDeviceIpRange>>(
                NetworkZoneQueries.getNetworkZoneDeviceIpRangeRoot, new { matrixId });
            Task<List<NetworkZoneDeviceIpRange>> internetPathsTask  = apiConnection.SendQueryAsync<List<NetworkZoneDeviceIpRange>>(
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