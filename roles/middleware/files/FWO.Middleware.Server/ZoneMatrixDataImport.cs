using FWO.Basics;
using FWO.Logging;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Logging;
using FWO.Services;
using FWO.Services.Logging;
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
        private const string PathFieldNameRoot = "path_to_root";
        private const string PathFieldNameInternet = "path_to_internet";
        private struct Counters
        {
            /// <summary>
            /// Gets the AllZones value.
            /// </summary>
            public int AllZones = 0;
            /// <summary>
            /// Gets the NewZoneSuccess value.
            /// </summary>
            public int NewZoneSuccess = 0;
            /// <summary>
            /// Gets the UpdateZoneSuccess value.
            /// </summary>
            public int UpdateZoneSuccess = 0;
            /// <summary>
            /// Gets the ZoneFail value.
            /// </summary>
            public int ZoneFail = 0;
            /// <summary>
            /// Gets the DeleteZoneSuccess value.
            /// </summary>
            public int DeleteZoneSuccess = 0;
            /// <summary>
            /// Gets the DeleteZoneFail value.
            /// </summary>
            public int DeleteZoneFail = 0;
            /// <summary>
            /// Gets the InsertConnection value.
            /// </summary>
            public int InsertConnection = 0;
            /// <summary>
            /// Gets the RemoveConnection value.
            /// </summary>
            public int RemoveConnection = 0;

            /// <summary>
            /// Initializes a new instance of the type.
            /// </summary>
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
            string responsMessage;
            try
            {
                ImportNwZoneMatrixData importedZoneMatrixData = JsonSerializer.Deserialize<ImportNwZoneMatrixData>(importFile) ?? throw new JsonException("File could not be parsed.");
                DeviceNameResolver deviceLookup = await DeviceNameResolver.ConstructAsync(apiConnection);
                CheckData(importedZoneMatrixData, deviceLookup);
                (MatrixId, ExistingZones) = await GetExistingMatrixWithZones(importedZoneMatrixData.Name);
                responsMessage = await ImportMatrix(importedZoneMatrixData, importfileName);
            }
            catch (Exception exc)
            {
                responsMessage = $"File {importfileName} could not be processed: {exc.Message}";
                Log.WriteError(LogMessageTitle, responsMessage);
                await AddLogEntry(GlobalConst.kImportZoneMatrixData, 2, LevelFile, responsMessage);
                failedImports.Add(importfileName);
            }
            return responsMessage;
        }

        private static void CheckData(ImportNwZoneMatrixData importedZoneMatrixData, DeviceNameResolver deviceLookup)
        {
            List<string> errorList = [];
            if (string.IsNullOrEmpty(importedZoneMatrixData.Name))
            {
                errorList.Add("No Matrix Name");
            }
            if (importedZoneMatrixData.NetworkZones.Select(z => z.Name).Distinct().ToList().Count != importedZoneMatrixData.NetworkZones.Count)
            {
                errorList.Add("Duplicate Zone Names");
            }
            if (importedZoneMatrixData.NetworkZones.Select(z => z.IdString).Distinct().ToList().Count != importedZoneMatrixData.NetworkZones.Count)
            {
                errorList.Add("Duplicate Zone IdStrings");
            }
            CheckCommunicationTargets(importedZoneMatrixData, errorList);
            CheckDeviceData(importedZoneMatrixData, deviceLookup, errorList);
            if (errorList.Count > 0)
            {
                throw new ArgumentException($"Errors during Matrix import;\n{string.Join("\n", errorList)}");
            }
        }

        /// <summary>
        /// Checks that every allowed communication names a zone the document defines.
        /// The auto-calculated zones are accepted because they are created by the import itself.
        /// </summary>
        private static void CheckCommunicationTargets(ImportNwZoneMatrixData importedZoneMatrixData, List<string> errorList)
        {
            HashSet<string> knownZones = [.. importedZoneMatrixData.NetworkZones.Select(zone => zone.IdString)];
            knownZones.Add(NetworkZoneService.kAutoCalculatedInternetZoneIdString);
            knownZones.Add(NetworkZoneService.kAutoCalculatedUndefinedInternalZoneIdString);

            foreach (NetworkZoneData zone in importedZoneMatrixData.NetworkZones)
            {
                foreach (CommunicationData communication in zone.CommData.Where(c => !knownZones.Contains(c.IdString)))
                {
                    errorList.Add($"Unknown communication target {communication.IdString} in zone {zone.IdString}");
                }
            }
        }

        private static void CheckDeviceData(ImportNwZoneMatrixData importedZoneMatrixData,
            DeviceNameResolver deviceLookup, List<string> errorList)
        {
            HashSet<string> ambiguous = [];
            HashSet<string> unknown = [];
            HashSet<string> duplicateRoot = [];
            HashSet<string> duplicateInternet = [];

            foreach (DeviceRefData device in ReferencedDevices(importedZoneMatrixData))
            {
                if (deviceLookup.IsAmbiguous(device.MgmtName, device.DeviceName))
                {
                    ambiguous.Add(DeviceNameResolver.Describe(device.MgmtName, device.DeviceName));
                }
                if (deviceLookup.Resolve(device.MgmtName, device.DeviceName) is null)
                {
                    unknown.Add(DeviceNameResolver.Describe(device.MgmtName, device.DeviceName));
                }
            }
            foreach (ZoneIpRangeData subnet in importedZoneMatrixData.NetworkZones.SelectMany(zone => zone.IpData))
            {
                CheckPathDuplicates(subnet, subnet.PathToRoot, duplicateRoot, PathFieldNameRoot);
                CheckPathDuplicates(subnet, subnet.PathToInternet, duplicateInternet, PathFieldNameInternet);
            }
            if (unknown.Count > 0)
            {
                errorList.Add($"Could not resolve devices {string.Join(", ", unknown)}");
            }
            if (ambiguous.Count > 0)
            {
                errorList.Add($"Devices {string.Join(", ", ambiguous)} are ambiguous");
            }
            errorList.AddRange(duplicateRoot);
            errorList.AddRange(duplicateInternet);
        }

        private static IEnumerable<DeviceRefData> ReferencedDevices(ImportNwZoneMatrixData matrixData)
        {
            return matrixData.NetworkZones
                .SelectMany(zone => zone.IpData)
                .SelectMany(subnet => subnet.PathToRoot.Concat(subnet.PathToInternet));
        }

        private static void CheckPathDuplicates(ZoneIpRangeData subnet,
            List<DeviceRefData> path, HashSet<string> duplicate, string pathFieldName)
        {
            HashSet<string> unique = [];

            foreach (DeviceRefData device in path)
            {
                string deviceText = DeviceNameResolver.Describe(device.MgmtName, device.DeviceName);
                if (!unique.Add(deviceText))
                {
                    duplicate.Add($"Duplicate device {deviceText} in {pathFieldName} in subnet {subnet.Ip}");
                }
            }
        }

        private async Task<string> ImportMatrix(ImportNwZoneMatrixData importedMatrix, string importfileName)
        {
            counters = new() { AllZones = importedMatrix.NetworkZones.Count };
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

            await NetworkZoneService.UpdateSpecialZones(MatrixId, apiConnection, globalConfig);

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
            await AddLogEntry(GlobalConst.kImportZoneMatrixData, 0, LevelFile, messageText);
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
                await ChangeLogHelper.LogMatrixChange(new MatrixChangeLogRequest
                {
                    Family = ChangeLogFamily.Import,
                    Operation = ChangeLogOperation.Create,
                    UserId = "Importer",
                    MatrixId = MatrixId,
                    MatrixName = MatrixName,
                    Origin = ChangeLogOrigin.Import
                });
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
                await AddLogEntry(GlobalConst.kImportZoneMatrixData, 1, LevelZone, errorText);
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
                await AddLogEntry(GlobalConst.kImportZoneMatrixData, 1, LevelZone, errorText);
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
                // Dont allow auto-calculated undefine-internal to have allowed communications.

                if (globalConfig.AutoCalculateInternetZone
                    && globalConfig.AutoCalculateUndefinedInternalZone
                    && incomingZoneData.CommData.Any(communication => communication.IdString == NetworkZoneService.kAutoCalculatedUndefinedInternalZoneIdString))
                {
                    throw new ArgumentException("Matrix contains allowed communication data for readonly auto-calculated undefined-internal zone.");
                }

                List<int> existDestZoneIds = [.. existingZone.AllowedCommunicationDestinations.Select(x => x.Id)];
                List<int> incomingDestZoneIds = [.. incomingZoneData.CommData.Select(x => ZoneIds[x.IdString])];

                NetworkZoneService.AdditionsDeletions addDel = new()
                {
                    DestinationZonesToAdd = incomingDestZoneIds.Except(existDestZoneIds).ToList().ConvertAll(i => new ComplianceNetworkZone() { Id = i }),
                    DestinationZonesToDelete = existDestZoneIds.Except(incomingDestZoneIds).ToList().ConvertAll(i => new ComplianceNetworkZone() { Id = i })
                };
                await NetworkZoneService.UpdateZone(existingZone, addDel, apiConnection);
                return (addDel.DestinationZonesToAdd.Count, addDel.DestinationZonesToDelete.Count);
            }
            return (0, 0);
        }

        private static IPAddressRange ConvertIpDataToAddressRange(ZoneIpRangeData importAreaIpData)
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
