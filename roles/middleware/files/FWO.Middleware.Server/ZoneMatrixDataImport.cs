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
        private const string LogMessageTitle = "Import Network Zone Data";
        private const string LevelFile = "Import File";
        private const string LevelZone = "Zone";

        /// <summary>
        /// Run a single Network Zone Matrix Data Import with uploaded data
        /// </summary>
        public async Task<List<string>> Run(string importFileName, string importedData)
        {
            List<string> FailedImports = [];
            importFile = importedData;
            await ImportSingleMatrix(importFileName, FailedImports);
            return FailedImports;
        }

        /// <summary>
        /// Run the Network Zone Matrix Data Import with a List of File paths to be read
        /// </summary>
        public async Task<List<string>> Run(List<string> importfilePathAndNames)
        {
            //List<string> importfilePathAndNames = JsonSerializer.Deserialize<List<string>>(globalConfig.ImportSubnetDataPath) ?? throw new JsonException("Config Data could not be deserialized.");
            List<string> FailedImports = [];

            // iterate over all files
            foreach (var importfilePathAndName in importfilePathAndNames)
            {
                // if (!RunImportScript(importfilePathAndName + ".py"))
                // {
                // 	Log.WriteInfo(LogMessageTitle, $"Script {importfilePathAndName}.py failed but trying to import from existing file.");
                // }
                await ImportSingleSource(importfilePathAndName, FailedImports);
            }
            return FailedImports;
        }

        private async Task ImportSingleSource(string importfileName, List<string> failedImports)
        {
            try
            {
                Log.WriteInfo(LogMessageTitle, $"Importing Area Network Data from file {importfileName}");
                ReadFile(importfileName);
                await ImportSingleMatrix(importfileName, failedImports);
            }
            catch (Exception exc)
            {
                string errorText = $"File {importfileName} could not be processed.";
                Log.WriteError(LogMessageTitle, errorText, exc);
                await AddLogEntry(GlobalConst.kImportNetorkZoneData, 2, LevelFile, errorText);
                failedImports.Add(importfileName);
            }
        }

        private async Task ImportSingleMatrix(string importfileName, List<string> failedImports)
        {
            try
            {
                ImportNwZoneMatrixData? importedZoneMatrixData = JsonSerializer.Deserialize<ImportNwZoneMatrixData>(importFile) ?? throw new JsonException("File could not be parsed.");
                if (importedZoneMatrixData != null && importedZoneMatrixData.NetworkZones != null)
                {
                    CheckData(importedZoneMatrixData);
                    await ImportMatrix(importedZoneMatrixData, importfileName);
                }
            }
            catch (Exception exc)
            {
                string errorText = $"File {importfileName} could not be processed.";
                Log.WriteError(LogMessageTitle, errorText, exc);
                await AddLogEntry(GlobalConst.kImportNetorkZoneData, 2, LevelFile, errorText);
                failedImports.Add(importfileName);
            }
        }

        private static void CheckData(ImportNwZoneMatrixData importedZoneMatrixData)
        {
            if (string.IsNullOrEmpty(importedZoneMatrixData.Name))
            {
                throw new ArgumentException("No Matrix Name");
            }
            if (importedZoneMatrixData.NetworkZones.Select(z => z.Name).Distinct().ToList().Count != importedZoneMatrixData.NetworkZones.Count)
            {
                throw new ArgumentException("Duplicate Zone names");
            }
        }

        private async Task ImportMatrix(ImportNwZoneMatrixData importedMatrix, string importfileName)
        {
            int successCounter = 0;
            int failCounter = 0;
            int insertConnectionCounter = 0;
            int removeConnectionCounter = 0;
            int deleteCounter = 0;
            int deleteFailCounter = 0;

            (MatrixId, ExistingZones) = await GetExistingMatrixWithZones(importfileName);
            if (MatrixId == 0)
            {
                await CreateMatrix(importedMatrix.Name, importedMatrix.Comment);
            }

            foreach (var incomingZone in importedMatrix.NetworkZones)
            {
                if (await SaveZone(incomingZone))
                {
                    ++successCounter;
                }
                else
                {
                    ++failCounter;
                }
            }
            foreach (var existingZone in ExistingZones.Where(z => importedMatrix.NetworkZones.FirstOrDefault(i => i.IdString == z.IdString) == null))
            {
                if (await DeactivateZone(existingZone))
                {
                    ++deleteCounter;
                }
                else
                {
                    ++deleteFailCounter;
                }
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
                insertConnectionCounter += inserts;
                removeConnectionCounter += removes;
            }
            string messageText = $"Imported from {importfileName}: {successCounter} network zones, {failCounter} failed. Deactivated {deleteCounter} network zones, {deleteFailCounter} failed. Inserted {insertConnectionCounter}, removed {removeConnectionCounter} connections.";
            Log.WriteInfo(LogMessageTitle, messageText);
            await AddLogEntry(GlobalConst.kImportNetorkZoneData, 0, LevelFile, messageText);
        }

        private async Task<(int, List<ComplianceNetworkZone>)> GetExistingMatrixWithZones(string importfileName)
        {
            List<ComplianceCriterion> existingMatrices = await apiConnection.SendQueryAsync<List<ComplianceCriterion>>(ComplianceQueries.getMatrixBySource, new { importSource = importfileName });
            if (existingMatrices.Count > 0)
            {
                int matrixId = existingMatrices[0].Id;
                return (matrixId, await apiConnection.SendQueryAsync<List<ComplianceNetworkZone>>(ComplianceQueries.getNetworkZonesForMatrix, new { criterionId = matrixId }));
            }
            return (0, []);
        }

        private async Task CreateMatrix(string MatrixName, string? comment)
        {
            ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(ComplianceQueries.addCriterion,
                new { name = MatrixName, comment = comment, criterionType = CriterionType.Matrix.ToString() })).ReturnIds;
            if (returnIds != null && returnIds.Length > 0)
            {
                MatrixId = returnIds[0].InsertedId;
            }
            else
            {
                throw new InternalException("Could not create Matrix");
            }
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
        }

        private async Task UpdateZone(NetworkZoneData incomingZoneData, ComplianceNetworkZone existingZone)
        {
            List<IPAddressRange> incomingRanges = incomingZoneData.IpData.ConvertAll(i => ConvertIpDataToAddressRange(i));
            NetworkZoneService.AdditionsDeletions addDel = new()
            {
                IpRangesToAdd = [.. incomingRanges.Except(existingZone.IPRanges)],
                IpRangesToDelete = [.. existingZone.IPRanges.Except(incomingRanges)]
            };
            await NetworkZoneService.UpdateZone(existingZone, addDel, apiConnection);
        }

        private async Task<bool> DeactivateZone(ComplianceNetworkZone zone)
        {
            try
            {
                await NetworkZoneService.RemoveZone(zone, apiConnection);
            }
            catch (Exception exc)
            {
                string errorText = $"Outdated Zone {zone.Name} could not be deleted.";
                Log.WriteError(LogMessageTitle, errorText, exc);
                await AddLogEntry(GlobalConst.kImportNetorkZoneData, 1, LevelZone, errorText);
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
                return (addDel.DestinationZonesToAdd.Count, addDel.DestinationZonesToAdd.Count);
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
