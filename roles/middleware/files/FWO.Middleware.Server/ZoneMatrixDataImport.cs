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
        private List<ComplianceNetworkZone> ExistingZones = [];
        private readonly Dictionary<string, int> ZoneIds = [];
        private int MatrixId = 0;
        private const string LogMessageTitle = "Import Network Zone Matrix Data";
        private const string LevelFile = "Import File";
        private const string LevelZone = "Zone";
        private const string PathFieldNameRoot = "path_to_root";
        private const string PathFieldNameInternet = "path_to_internet";
        /// <summary>
        /// Bulk import into network_zone.device_ip_range_root and network_zone.device_ip_range_internet
        /// gets chunked with this size.
        /// </summary>
        protected const int kPathInsertBatchSize = 500;
        /// <summary>
        /// Counts database inserts, removals and failures during matrix import.
        /// </summary>
        private class ImportCounters
        {
            public int AllZones;
            public int NewZoneSuccess;
            public int UpdateZoneSuccess;
            public int ZoneFail;
            public int DeleteZoneSuccess;
            public int DeleteZoneFail;
            public int InsertConnection;
            public int RemoveConnection;
            public int InsertPathRoot;
            public int InsertPathInternet;
            public int RemovePathRoot;
            public int RemovePathInternet;
        }
        private ImportCounters counters = new();
        /// <summary>
        /// Input format for bulk import of paths. Order is later transformed to order_to_root/order_to_internet respectively.
        /// </summary>
        private sealed record NetworkZoneDeviceIpRangeInsertInput(int DeviceId, int IpRangeId, int Order);

        /// <summary>
        /// Run a single Network Zone Matrix Data Import with uploaded data
        /// </summary>
        public async Task<string> Run(string importFileName, string importedData, string userName, string userDn)
        {
            importFile = importedData;
            Log.WriteAudit(
                Title: $"Compliance Matrix Import",
                Text: $"Run import from {importFileName}",
                UserName: userName,
                UserDN: userDn);

            return await ImportSingleMatrix(importFileName);
        }

        /// <summary>
        /// Top level method to import a matrix; calls validation, get existing data and import input matrix.
        /// </summary>
        private async Task<string> ImportSingleMatrix(string importFileName)
        {
            string responseMessage;
            try
            {
                ImportNwZoneMatrixData importedZoneMatrixData = JsonSerializer.Deserialize<ImportNwZoneMatrixData>(importFile) ?? throw new JsonException("File could not be parsed.");
                DeviceNameResolver deviceLookup = await DeviceNameResolver.ConstructAsync(apiConnection);
                CheckData(importedZoneMatrixData, deviceLookup, globalConfig);
                (MatrixId, ExistingZones) = await GetExistingMatrixWithZones(importedZoneMatrixData.Name);
                responseMessage = await ImportMatrix(importedZoneMatrixData, importFileName, deviceLookup);
            }
            catch (Exception exc)
            {
                responseMessage = $"File {importFileName} could not be processed: {exc.Message}";
                Log.WriteError(LogMessageTitle, responseMessage);
                await AddLogEntry(GlobalConst.kImportZoneMatrixData, 2, LevelFile, responseMessage);
            }
            return responseMessage;
        }

        /// <summary>
        /// Top level validation method. Simple checks are done here, more complicated checks are called.
        /// Failed checks don't interrupt validation but add to an error list, which interrupts if exists and is displayed in the end.
        /// </summary>
        private static void CheckData(ImportNwZoneMatrixData importedZoneMatrixData, DeviceNameResolver deviceLookup, GlobalConfig globalConfig)
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
            CheckReservedZoneIds(importedZoneMatrixData, errorList);
            CheckDuplicateSubnet(importedZoneMatrixData, errorList);
            CheckCommunicationTargets(importedZoneMatrixData, errorList, globalConfig);
            CheckDeviceData(importedZoneMatrixData, deviceLookup, errorList);
            CheckIpData(importedZoneMatrixData, errorList);
            if (errorList.Count > 0)
            {
                throw new ArgumentException($"Errors during Matrix import;\n{string.Join("\n", errorList)}");
            }
        }

        /// <summary>
        /// Finds subnets that are exactly the same in a zone and calls them out.
        /// </summary>
        private static void CheckDuplicateSubnet(ImportNwZoneMatrixData importedZoneMatrixData, List<string> errorList)
        {
            foreach (NetworkZoneData zone in importedZoneMatrixData.NetworkZones)
            {
                HashSet<IPAddressRange> subnetsInZone = [];
                foreach (ZoneIpRangeData subnet in zone.IpData)
                {
                    if (!TryConvertIpDataToAddressRange(subnet, out IPAddressRange range))
                    {
                        continue;
                    }
                    if (!subnetsInZone.Add(range))
                    {
                        errorList.Add($"Duplicate subnet with IP {subnet.Ip} in zone {zone.IdString}.");
                    }
                }
            }
        }

        /// <summary>
        /// Checks that internal zone names are not used by customer.
        /// </summary>
        private static void CheckReservedZoneIds(ImportNwZoneMatrixData importedZoneMatrixData, List<string> errorList)
        {
            HashSet<string> knownZones = [.. importedZoneMatrixData.NetworkZones.Select(zone => zone.IdString)];
            if (knownZones.Contains(NetworkZoneService.kAutoCalculatedInternetZoneIdString))
            {
                errorList.Add($"Use of internally reserved zone {NetworkZoneService.kAutoCalculatedInternetZoneIdString} - please use a different id_string for your zone");
            }
            if (knownZones.Contains(NetworkZoneService.kAutoCalculatedUndefinedInternalZoneIdString))
            {
                errorList.Add($"Use of internally reserved zone {NetworkZoneService.kAutoCalculatedUndefinedInternalZoneIdString} - please use a different id_string for your zone");
            }
        }

        /// <summary>
        /// Checks that every allowed communication names a zone the document defines.
        /// The auto-calculated internet zone is accepted while it is enabled, because the import creates it itself.
        /// </summary>
        private static void CheckCommunicationTargets(ImportNwZoneMatrixData importedZoneMatrixData, List<string> errorList, GlobalConfig globalConfig)
        {
            HashSet<string> knownZones = [.. importedZoneMatrixData.NetworkZones.Select(zone => zone.IdString)];
            if (globalConfig.AutoCalculateInternetZone)
            {
                knownZones.Add(NetworkZoneService.kAutoCalculatedInternetZoneIdString);
            }

            foreach (NetworkZoneData zone in importedZoneMatrixData.NetworkZones)
            {
                foreach (CommunicationData communication in zone.CommData.Where(c => !knownZones.Contains(c.IdString)))
                {
                    errorList.Add($"Unknown communication target {communication.IdString} in zone {zone.IdString}");
                }
            }
        }

        /// <summary>
        /// Checks subnet paths to root and internet. Management + Device combinations
        /// need to be well defined in database
        /// </summary>
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

        /// <summary>
        /// Helper that creates an iteration over all Management + Device combinations in input matrix paths
        /// </summary>
        private static IEnumerable<DeviceRefData> ReferencedDevices(ImportNwZoneMatrixData matrixData)
        {
            return matrixData.NetworkZones
                .SelectMany(zone => zone.IpData)
                .SelectMany(subnet => subnet.PathToRoot.Concat(subnet.PathToInternet));
        }

        /// <summary>
        /// Takes a path and checks for duplicate devices
        /// </summary>
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

        /// <summary>
        /// Checks if all IPs used in input matrix subnets are resolvable.
        /// </summary>
        private static void CheckIpData(ImportNwZoneMatrixData importedZoneMatrixData, List<string> errorList)
        {
            foreach (NetworkZoneData zone in importedZoneMatrixData.NetworkZones)
            {
                foreach (ZoneIpRangeData subnet in zone.IpData)
                {
                    if (!TryConvertIpDataToAddressRange(subnet, out _))
                    {
                        errorList.Add($"Bad Ips for subnet {subnet.Ip} in zone {zone.Name}");
                    }
                }
            }
        }

        /// <summary>
        /// Imports one matrix: creates it when no matrix of that name exists yet, otherwise updates its
        /// metadata, then writes the zones of the import file, deactivates the zones the file no longer
        /// names, and recalculates the auto-calculated zones.
        /// </summary>
        private async Task<string> ImportMatrix(ImportNwZoneMatrixData importedMatrix, string importFileName, DeviceNameResolver deviceLookup)
        {
            counters = new() { AllZones = importedMatrix.NetworkZones.Count };
            ZoneIds.Clear();
            if (MatrixId == 0)
            {
                await CreateMatrix(importedMatrix.Name, importFileName, importedMatrix.Comment);
            }
            else
            {
                await UpdateMatrix(importFileName, importedMatrix.Comment);
            }

            foreach (var incomingZone in importedMatrix.NetworkZones)
            {
                await SaveZone(incomingZone);
            }

            foreach (var existingZone in ExistingZones.Where(z =>
                !z.IsAutoCalculatedInternetZone && !z.IsAutoCalculatedUndefinedInternalZone
                && importedMatrix.NetworkZones.FirstOrDefault(i => i.IdString == z.IdString) == null))
            {
                await DeactivateZone(existingZone);
            }

            await NetworkZoneService.UpdateSpecialZones(MatrixId, apiConnection, globalConfig);

            // Reload with all ids: the import file carries id_strings, the API assigns the ids that
            // SaveZoneConnections and HandleIpRangePaths address zones by.
            ExistingZones = await apiConnection.SendQueryAsync<List<ComplianceNetworkZone>>(NetworkZoneQueries.getNetworkZonesForMatrix, new { criterionId = MatrixId });
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

            await HandleIpRangePaths(importedMatrix, deviceLookup);

            string messageText = ConstructMessageText(importFileName);
            Log.WriteInfo(LogMessageTitle, messageText);
            await AddLogEntry(GlobalConst.kImportZoneMatrixData, 0, LevelFile, messageText);
            return messageText;
        }

        /// <summary>
        /// Builds message with statistics of inserted and deleted database entries.
        /// </summary>
        private string ConstructMessageText(string importFileName)
        {
            return $"Ok: Imported from {importFileName}: Total number of network zones: {counters.AllZones}, " +
                $"new: {counters.NewZoneSuccess}, updated: {counters.UpdateZoneSuccess}, failed: {counters.ZoneFail}. " +
                $"Deleted: {counters.DeleteZoneSuccess}, failed deletions: {counters.DeleteZoneFail}. " +
                $"Inserted connections: {counters.InsertConnection}, removed connections: {counters.RemoveConnection}. " +
                $"Inserted paths to root: {counters.InsertPathRoot}, removed paths to root: {counters.RemovePathRoot}. " +
                $"Inserted paths to internet: {counters.InsertPathInternet}, removed paths to internet: {counters.RemovePathInternet}.";
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
                return (matrixId, await apiConnection.SendQueryAsync<List<ComplianceNetworkZone>>(NetworkZoneQueries.getNetworkZonesForMatrix, new { criterionId = matrixId }));
            }
            return (0, []);
        }

        private async Task CreateMatrix(string matrixName, string importFileName, string? comment)
        {
            ReturnId[]? returnIds = (await apiConnection.SendQueryAsync<ReturnIdWrapper>(ComplianceQueries.addCriterion,
                new { name = matrixName, importSource = importFileName, comment = comment, criterionType = CriterionType.Matrix.ToString() })).ReturnIds;
            if (returnIds != null && returnIds.Length > 0)
            {
                MatrixId = returnIds[0].InsertedId;
                await ChangeLogHelper.LogMatrixChange(new MatrixChangeLogRequest
                {
                    Family = ChangeLogFamily.Import,
                    Operation = ChangeLogOperation.Create,
                    UserId = "Importer",
                    MatrixId = MatrixId,
                    MatrixName = matrixName,
                    Origin = ChangeLogOrigin.Import
                });
            }
            else
            {
                throw new InternalException("Could not create Matrix");
            }
        }

        private async Task UpdateMatrix(string importFileName, string? comment)
        {
            await apiConnection.SendQueryAsync<ReturnIdWrapper>(ComplianceQueries.updateCriterionMetadata,
                new { id = MatrixId, importSource = importFileName, comment = comment });
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

        private async Task<(int Inserted, int Removed)> SaveZoneConnections(NetworkZoneData incomingZoneData)
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

        /// <summary>
        /// Deletes all old ip range paths from database and adds all paths from input matrix.
        /// </summary>
        private async Task HandleIpRangePaths(ImportNwZoneMatrixData importedMatrix, DeviceNameResolver deviceLookup)
        {
            List<NetworkZoneIpRange> ipRanges = await apiConnection.SendQueryAsync<List<NetworkZoneIpRange>>(
                NetworkZoneQueries.getIpRangesForMatrix, new { matrixId = MatrixId });
            Dictionary<(int ZoneId, IPAddressRange Range), int> ipRangeIds = [];
            foreach (NetworkZoneIpRange range in ipRanges)
            {
                ipRangeIds[(range.NetworkZoneId,
                    new IPAddressRange(ParseAddress(range.IpRangeStart), ParseAddress(range.IpRangeEnd)))] = range.Id;
            }

            List<NetworkZoneDeviceIpRangeInsertInput> rootPathInput = [];
            List<NetworkZoneDeviceIpRangeInsertInput> internetPathInput = [];

            foreach (NetworkZoneData zone in importedMatrix.NetworkZones)
            {
                if (!ZoneIds.TryGetValue(zone.IdString, out int zoneId))
                {
                    Log.WriteWarning(LogMessageTitle,
                        $"No zone {zone.IdString} in matrix {MatrixId}, skipping its ip range paths.");
                    continue;
                }
                foreach (ZoneIpRangeData subnet in zone.IpData)
                {
                    if (!ipRangeIds.TryGetValue((zoneId, ConvertIpDataToAddressRange(subnet)), out int ipRangeId))
                    {
                        Log.WriteWarning(LogMessageTitle,
                            $"Could not resolve ip range with start IP {subnet.Ip} in zone {zone.IdString}, skipping its paths.");
                        continue;
                    }
                    rootPathInput.AddRange(BuildPathItems(subnet.PathToRoot, ipRangeId, deviceLookup, PathFieldNameRoot, subnet.Ip));
                    internetPathInput.AddRange(BuildPathItems(subnet.PathToInternet, ipRangeId, deviceLookup, PathFieldNameInternet, subnet.Ip));
                }
            }

            counters.RemovePathRoot = (await apiConnection.SendQueryAsync<ReturnId>
                (NetworkZoneQueries.deleteNetworkZoneDeviceIpRangeRoot, new { matrixId = MatrixId })).AffectedRows;
            counters.RemovePathInternet = (await apiConnection.SendQueryAsync<ReturnId>
                (NetworkZoneQueries.deleteNetworkZoneDeviceIpRangeInternet, new { matrixId = MatrixId })).AffectedRows;

            counters.InsertPathRoot = (await apiConnection.SendQueryAsync<ReturnId>
                (NetworkZoneQueries.addPathItemsRoot,
                new
                {
                    objects = rootPathInput.Select(item => new
                    {
                        dev_id = item.DeviceId,
                        ip_range_id = item.IpRangeId,
                        order_to_root = item.Order
                    }).ToList()
                },
                chunkingOptions: new QueryChunkingOptions
                {
                    Enabled = true,
                    ChunkVariableName = "objects",
                    ChunkSize = kPathInsertBatchSize,
                    MergeMode = ChunkMergeMode.MutationAffectedRowsOnly
                })).AffectedRows;

            counters.InsertPathInternet = (await apiConnection.SendQueryAsync<ReturnId>
                (NetworkZoneQueries.addPathItemsInternet,
                new
                {
                    objects = internetPathInput.Select(item => new
                    {
                        dev_id = item.DeviceId,
                        ip_range_id = item.IpRangeId,
                        order_to_internet = item.Order
                    }).ToList()
                },
                chunkingOptions: new QueryChunkingOptions
                {
                    Enabled = true,
                    ChunkVariableName = "objects",
                    ChunkSize = kPathInsertBatchSize,
                    MergeMode = ChunkMergeMode.MutationAffectedRowsOnly
                })).AffectedRows;
        }

        /// <summary>
        /// Parses a single address from an inet value, which the API returns with a prefix like "10.0.0.1/32".
        /// </summary>
        private static IPAddress ParseAddress(string address) => IPAddressRange.Parse(address).Begin;

        /// <summary>
        /// Loops over devices in a path and adds it to the bulk import list. In case a device is not
        /// resolvable, the path is skipped with a warning.
        /// </summary>
        private static List<NetworkZoneDeviceIpRangeInsertInput> BuildPathItems(
            List<DeviceRefData> path, int ipRangeId, DeviceNameResolver deviceLookup, string pathFieldName, string subnetIp)
        {
            List<NetworkZoneDeviceIpRangeInsertInput> pathInput = [];
            foreach ((int index, DeviceRefData device) in path.Index())
            {
                if (deviceLookup.Resolve(device.MgmtName, device.DeviceName) is not int deviceId)
                {
                    Log.WriteWarning(LogMessageTitle,
                        $"Could not resolve {DeviceNameResolver.Describe(device.MgmtName, device.DeviceName)} in {pathFieldName} in subnet {subnetIp}, skipping its path.");
                    return [];
                }
                pathInput.Add(new NetworkZoneDeviceIpRangeInsertInput(deviceId, ipRangeId, index + 1));
            }
            return pathInput;
        }

        /// <summary>
        /// Converts imported ip data into an address range.
        /// </summary>
        private static IPAddressRange ConvertIpDataToAddressRange(ZoneIpRangeData importAreaIpData)
        {
            return TryConvertIpDataToAddressRange(importAreaIpData, out IPAddressRange range)
                ? range
                : throw new ArgumentException($"Invalid ip data: {importAreaIpData.Ip}");
        }

        /// <summary>
        /// Converts imported ip data into an address range. Returns false if the data cannot be parsed.
        /// </summary>
        private static bool TryConvertIpDataToAddressRange(ZoneIpRangeData importAreaIpData, out IPAddressRange range)
        {
            range = default!;
            string ip = importAreaIpData.Ip;
            string? ipEnd = importAreaIpData.IpEnd;
            if (string.IsNullOrWhiteSpace(ipEnd))
            {
                if (!ip.TryParseIPStringToRange(out (string start, string end) parsed))
                {
                    return false;
                }
                (ip, ipEnd) = parsed;
            }
            if (!IPAddress.TryParse(ip, out IPAddress? start) || !IPAddress.TryParse(ipEnd ?? ip, out IPAddress? end))
            {
                return false;
            }
            if (IpOperations.CompareIpFamilies(start, end) != 0)
            {
                return false;
            }
            if (IpOperations.CompareIpValues(start, end) > 0)
            {
                return false;
            }
            range = new IPAddressRange(start, end);
            return true;
        }
    }
}
