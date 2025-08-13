using FWO.Basics;
using FWO.Logging;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Services;
using System.Text.Json;
using FWO.Basics.Exceptions;
using NetTools;
using System.Net;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Class handling the Network Zone Matrix Data Import
    /// </summary>
    public class ZoneMatrixDataImport(ApiConnection apiConnection, GlobalConfig globalConfig) : DataImportBase(apiConnection, globalConfig)
    {
        List<ComplianceNetworkZone> ExistingZones = [];
        readonly Dictionary<string, int> ZoneIds = [];
        int MatrixId = 0;
        private const string LogMessageTitle = "Import Network Zone Matrix Data";
        private const string LevelFile = "Import File";
        private const string LevelZone = "Zone";
        private struct Counters
        {
            public int AllZones = 0;
            public int NewZoneSuccess = 0;
            public int UpdateZoneSuccess = 0;
            public int ZoneFail = 0;
            public int DeleteZoneSuccess = 0;
            public int DeleteZoneFail = 0;
            public int InsertConnection = 0;
            public int RemoveConnection = 0;

            public Counters() { }
        }
        Counters counters = new();

        /// <summary>
        /// Run a single Network Zone Matrix Data Import with uploaded data
        /// </summary>
        public async Task<string> Run(string importFileName, string importedData, string userName, string userDn)
        {
            List<string> FailedImports = [];
            importFile = importedData;
            Log.WriteAudit(
                Title: $"Compliance Matrix Import",
                Text: $"Run import from {importFileName}",
                UserName: userName,
                UserDN: userDn);

            return await ImportSingleMatrix(importFileName, FailedImports);
        }

        private async Task<string> ImportSingleMatrix(string importfileName, List<string> failedImports)
        {
            string responsMessage = "";
            try
            {
                ImportNwZoneMatrixData? importedZoneMatrixData = JsonSerializer.Deserialize<ImportNwZoneMatrixData>(importFile) ?? throw new JsonException("File could not be parsed.");
                if (importedZoneMatrixData != null && importedZoneMatrixData.NetworkZones != null)
                {
                    CheckData(importedZoneMatrixData);
                    (MatrixId, ExistingZones) = await GetExistingMatrixWithZones(importedZoneMatrixData.Name);
                    responsMessage = await ImportMatrix(importedZoneMatrixData, importfileName);
                }
            }
            catch (Exception exc)
            {
                responsMessage = $"File {importfileName} could not be processed: {exc.Message}";
                Log.WriteError(LogMessageTitle, responsMessage);
                await AddLogEntry(GlobalConst.kImportNetorkZoneData, 2, LevelFile, responsMessage);
                failedImports.Add(importfileName);
            }
            return responsMessage;
        }

        private static void CheckData(ImportNwZoneMatrixData importedZoneMatrixData)
        {
            if (string.IsNullOrEmpty(importedZoneMatrixData.Name))
            {
                throw new ArgumentException("No Matrix Name");
            }
            if (importedZoneMatrixData.NetworkZones.Select(z => z.Name).Distinct().ToList().Count != importedZoneMatrixData.NetworkZones.Count)
            {
                throw new ArgumentException("Duplicate Zone Names");
            }
            if (importedZoneMatrixData.NetworkZones.Select(z => z.IdString).Distinct().ToList().Count != importedZoneMatrixData.NetworkZones.Count)
            {
                throw new ArgumentException("Duplicate Zone IdStrings");
            }
        }

        private async Task<string> ImportMatrix(ImportNwZoneMatrixData importedMatrix, string importfileName)
        {
            counters = new(){ AllZones = importedMatrix.NetworkZones.Count };
            if (MatrixId == 0)
            {
                await CreateMatrix(importedMatrix.Name, importfileName, importedMatrix.Comment);
            }
            else
            {
                await UpdateMatrix(importfileName, importedMatrix.Comment);
            }

            foreach (var incomingZone in importedMatrix.NetworkZones)
            {
                await SaveZone(incomingZone);
            }

            foreach (var existingZone in ExistingZones.Where(z => importedMatrix.NetworkZones.FirstOrDefault(i => i.IdString == z.IdString) == null))
            {
                await DeactivateZone(existingZone);
            }

            // Reload existing zones with all Ids
            ExistingZones = await apiConnection.SendQueryAsync<List<ComplianceNetworkZone>>(ComplianceQueries.getNetworkZonesForMatrix, new { criterionId = MatrixId });
            foreach (var zone in ExistingZones)
            {
                ZoneIds.Add(zone.IdString, zone.Id);
            }
            foreach (var incomingZone in importedMatrix.NetworkZones)
            {
                (int inserts, int removes) = await SaveZoneConnections(incomingZone);
                counters.InsertConnection += inserts;
                counters.RemoveConnection += removes;
            }

            string messageText = ConstructMessageText(importfileName);
            Log.WriteInfo(LogMessageTitle, messageText);
            await AddLogEntry(GlobalConst.kImportNetorkZoneData, 0, LevelFile, messageText);
            return messageText;
        }

        private string ConstructMessageText(string importfileName)
        {
            return $"Ok: Imported from {importfileName}: Total number of network zones: {counters.AllZones}, " +
                $"new: {counters.NewZoneSuccess}, updated: {counters.UpdateZoneSuccess}, failed: {counters.ZoneFail}. " +
                $"Deleted: {counters.DeleteZoneSuccess}, failed deletions: {counters.DeleteZoneFail}. " +
                $"Inserted connections: {counters.InsertConnection}, removed connections: {counters.RemoveConnection}.";
        }

        private async Task<(int, List<ComplianceNetworkZone>)> GetExistingMatrixWithZones(string matrixName)
        {
            List<ComplianceCriterion> existingMatrices = await apiConnection.SendQueryAsync<List<ComplianceCriterion>>(ComplianceQueries.getMatrixByName, new { name = matrixName });
            if (existingMatrices.Count > 0)
            {
                if (string.IsNullOrEmpty(existingMatrices[0].ImportSource))
                {
                    throw new ArgumentException("Manually created matrix existing with same Name");
                }
                int matrixId = existingMatrices[0].Id;
                return (matrixId, await apiConnection.SendQueryAsync<List<ComplianceNetworkZone>>(ComplianceQueries.getNetworkZonesForMatrix, new { criterionId = matrixId }));
            }
            return (0, []);
        }

        private async Task CreateMatrix(string MatrixName, string importfileName, string? comment)
        {
            ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(ComplianceQueries.addCriterion,
                new { name = MatrixName, importSource = importfileName, comment = comment, criterionType = CriterionType.Matrix.ToString() })).ReturnIds;
            if (returnIds != null && returnIds.Length > 0)
            {
                MatrixId = returnIds[0].InsertedId;
            }
            else
            {
                throw new InternalException("Could not create Matrix");
            }
        }

        private async Task UpdateMatrix(string importfileName, string? comment)
        {
            await apiConnection.SendQueryAsync<ReturnIdWrapper>(ComplianceQueries.updateCriterionMetadata,
                new { id = MatrixId, importSource = importfileName, comment = comment });
        }

        private async Task<bool> SaveZone(NetworkZoneData incomingZone)
        {
            try
            {
                ComplianceNetworkZone? existingZone = ExistingZones.FirstOrDefault(x => x.IdString == incomingZone.IdString);
                if (existingZone == null)
                {
                    await NewZone(incomingZone);
                }
                else
                {
                    await UpdateZone(incomingZone, existingZone);
                }
            }
            catch (Exception exc)
            {
                string errorText = $"Zone {incomingZone.Name}({incomingZone.IdString}) could not be processed.";
                Log.WriteError(LogMessageTitle, errorText, exc);
                await AddLogEntry(GlobalConst.kImportNetorkZoneData, 1, LevelZone, errorText);
                counters.ZoneFail++;
                return false;
            }
            return true;
        }

        private async Task NewZone(NetworkZoneData incomingZoneData)
        {
            ComplianceNetworkZone incomingZone = new()
            {
                CriterionId = MatrixId,
                Name = incomingZoneData.Name,
                IdString = incomingZoneData.IdString
            };
            NetworkZoneService.AdditionsDeletions addDel = new()
            {
                IpRangesToAdd = incomingZoneData.IpData.ConvertAll(i => ConvertIpDataToAddressRange(i))
            };
            await NetworkZoneService.AddZone(incomingZone, addDel, apiConnection);
            counters.NewZoneSuccess++;
        }

        private async Task UpdateZone(NetworkZoneData incomingZoneData, ComplianceNetworkZone existingZone)
        {
            List<IPAddressRange> incomingRanges = incomingZoneData.IpData.ConvertAll(i => ConvertIpDataToAddressRange(i));
            NetworkZoneService.AdditionsDeletions addDel = new()
            {
                IpRangesToAdd = [.. incomingRanges.Except(existingZone.IPRanges)],
                IpRangesToDelete = [.. existingZone.IPRanges.Except(incomingRanges)]
            };
            if (addDel.IpRangesToAdd.Count > 0 || addDel.IpRangesToDelete.Count > 0 || existingZone.Name != incomingZoneData.Name)
            {
                existingZone.Name = incomingZoneData.Name;
                await NetworkZoneService.UpdateZone(existingZone, addDel, apiConnection);
                counters.UpdateZoneSuccess++;
            }
        }

        private async Task<bool> DeactivateZone(ComplianceNetworkZone zone)
        {
            try
            {
                await NetworkZoneService.RemoveZone(zone, apiConnection);
                counters.DeleteZoneSuccess++;
            }
            catch (Exception exc)
            {
                string errorText = $"Outdated Zone {zone.Name} could not be deleted.";
                Log.WriteError(LogMessageTitle, errorText, exc);
                await AddLogEntry(GlobalConst.kImportNetorkZoneData, 1, LevelZone, errorText);
                counters.DeleteZoneFail++;
                return false;
            }
            return true;
        }

        private async Task<(int, int)> SaveZoneConnections(NetworkZoneData incomingZoneData)
        {
            ComplianceNetworkZone? existingZone = ExistingZones.FirstOrDefault(x => x.IdString == incomingZoneData.IdString);
            if (existingZone != null)
            {
                List<int> existDestZoneIds = [.. existingZone.AllowedCommunicationDestinations.Select(x => x.Id)];
                List<int> incomingDestZoneIds = [.. incomingZoneData.CommData.Select(x => ZoneIds[x.IdString])];
                NetworkZoneService.AdditionsDeletions addDel = new()
                {
                    DestinationZonesToAdd = incomingDestZoneIds.Except(existDestZoneIds).ToList().ConvertAll(i => new ComplianceNetworkZone(){ Id = i }),
                    DestinationZonesToDelete = existDestZoneIds.Except(incomingDestZoneIds).ToList().ConvertAll(i => new ComplianceNetworkZone(){ Id = i })
                };
                await NetworkZoneService.UpdateZone(existingZone, addDel, apiConnection);
                return (addDel.DestinationZonesToAdd.Count, addDel.DestinationZonesToDelete.Count);
            }
            return (0, 0);
        }

        private static IPAddressRange ConvertIpDataToAddressRange(ModellingImportAreaIpData importAreaIpData)
        {
            string Ip = importAreaIpData.Ip;
            string? IpEnd = importAreaIpData.IpEnd;
            if (string.IsNullOrEmpty(importAreaIpData.IpEnd))
            {
                (Ip, IpEnd) = IpOperations.SplitIpToRange(importAreaIpData.Ip);
            }
            return new(IPAddress.Parse(Ip), IPAddress.Parse(IpEnd ?? Ip));
        }
    }
}
