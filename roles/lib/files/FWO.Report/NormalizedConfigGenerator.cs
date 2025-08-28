using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Report;
using FWO.Logging;
using FWO.Report.Filter;
using Newtonsoft.Json;

namespace FWO.Report
{
    public static class NormalizedConfigGenerator
    {
        public static async Task<NormalizedConfig> Generate(List<int> managementIds, string? configTime, ApiConnection apiConnection)
        {
            if (string.IsNullOrEmpty(configTime))
            {
                Dictionary<string, object> queryVars = new()
                {
                    { "mgm_ids", managementIds }
                };
                List<Management> managementData = await apiConnection.SendQueryAsync<List<Management>>(ReportQueries.getManagementForLatestNormalizedConfig, queryVars);
                if (managementData.Count == 0)
                {
                    throw new ArgumentException("No management data found for the given management IDs.");
                }
                return ParseFromManagementData(managementData[0]);
            }
            // TODO: Implement configTime handling
            throw new NotImplementedException("configTime handling not yet implemented.");
        }

        public static async Task<List<ManagementReport>> GetRelevantImportId(ApiConnection apiConnection, int managementId, string configTime)
        {
            DateTime time;
            try
            {
                time = DateTime.ParseExact(configTime, DynGraphqlQuery.fullTimeFormat, null);
            }
            catch (FormatException)
            {
                Log.WriteError("GetRelevantImportId", $"Invalid timestamp format.", null);
                throw new ArgumentException($"Invalid timestamp format. Please use {DynGraphqlQuery.fullTimeFormat}.");
            }
            Dictionary<string, object> ImpIdQueryVariables = new()
            {
                [QueryVar.Time] = time.ToString(DynGraphqlQuery.fullTimeFormat),
                [QueryVar.MgmIds] = managementId
            };
            return await apiConnection.SendQueryAsync<List<ManagementReport>>(ReportQueries.getRelevantImportIdsAtTime, ImpIdQueryVariables);
        }

        public static NormalizedConfig ParseFromManagementData(Management managementData)
        {
            var normalizedConfig = new NormalizedConfig
            {
                ConfigFormat = "NORMALIZED_LEGACY",
                Action = "INSERT",
                NetworkObjects = managementData.Objects.ToDictionary(
                    nwobj => nwobj.Uid,
                    NormalizedNetworkObject.FromNetworkObject
                ),
                ServiceObjects = managementData.Services.ToDictionary(
                    svc => svc.Uid,
                    NormalizedServiceObject.FromNetworkService
                ),
                Users = [], // TODO: implement
                ZoneObjects = managementData.Zones.ToDictionary(
                    zone => zone.Name,
                    NormalizedZoneObject.FromZoneObject
                ),
                Rulebases = [.. managementData.Rulebases.Select(rb => NormalizedRulebase.FromRulebase(rb, managementData.Uid ?? ""))],
                Gateways = [.. managementData.Devices.Select(NormalizedGateway.FromDevice)]
            };

            return normalizedConfig;
        }
    }
}