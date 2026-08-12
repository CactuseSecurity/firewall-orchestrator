using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using System.Net;
using System.Text.Json;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Imports normalized logging data produced by customization scripts.
    /// </summary>
    public class LogDataImport(ApiConnection apiConnection, GlobalConfig globalConfig) : DataImportBase(apiConnection, globalConfig)
    {
        private const string LogMessageTitle = "Import Log Data";
        private const string LevelFile = "Import File";
        private const int TcpProtocol = 6;
        private const int UdpProtocol = 17;

        /// <summary>
        /// Runs configured log data imports and removes expired entries.
        /// </summary>
        /// <returns>Sources which could not be imported.</returns>
        public async Task<List<string>> Run()
        {
            List<string> sources = JsonSerializer.Deserialize<List<string>>(globalConfig.ImportLogDataPath)
                ?? throw new JsonException("Log data import sources could not be deserialized.");
            List<string> failedImports = new();

            foreach (string source in sources)
            {
                await ImportSource(source, failedImports);
            }

            await DeleteExpiredEntries();
            return failedImports;
        }

        /// <summary>
        /// Validates external log data and converts it into database input values.
        /// </summary>
        public static List<FirewallLogEntryInput> NormalizeEntries(IEnumerable<LogDataImportEntry> entries, int maxEntries, DateTimeOffset importTime)
        {
            List<FirewallLogEntryInput> normalizedEntries = new();
            foreach (LogDataImportEntry entry in entries)
            {
                normalizedEntries.Add(NormalizeEntry(entry, importTime));
            }

            int entryLimit = Math.Max(0, maxEntries);
            return normalizedEntries
                .OrderByDescending(entry => entry.LogCount)
                .Take(entryLimit)
                .ToList();
        }

        private async Task ImportSource(string configuredSource, List<string> failedImports)
        {
            string sourcePath = ImportPathPolicy.RemoveAllowedExtension(configuredSource);
            List<string> importFiles = ValidateConfiguredImportSource(sourcePath);
            string scriptPath = sourcePath + ".py";
            if (importFiles.Contains(scriptPath) && !RunImportScript(scriptPath, globalConfig.ImportLogDataScriptArgs))
            {
                Log.WriteInfo(LogMessageTitle, $"Script {scriptPath} failed but trying existing JSON data.");
            }

            try
            {
                ReadFile(sourcePath + ".json");
                LogDataImportFile importFileData = JsonSerializer.Deserialize<LogDataImportFile>(importFile)
                    ?? throw new JsonException("Log data file could not be parsed.");
                await SaveEntries(importFileData.Logs, sourcePath);
                AcknowledgeImport(scriptPath, importFiles);
            }
            catch (Exception exception)
            {
                string message = $"Log data source {sourcePath}.json could not be processed.";
                Log.WriteError(LogMessageTitle, message, exception);
                await AddLogEntry(GlobalConst.kImportLogData, 2, LevelFile, message);
                failedImports.Add(sourcePath);
            }
        }

        /// <summary>
        /// Merges entries describing the same flow of the same owner into a single entry.
        /// The database keeps one row per owner, source, destination and service, so one batch
        /// must not contain the same flow twice.
        /// </summary>
        /// <returns>The entries without duplicated flows.</returns>
        public static List<FirewallLogEntryInput> MergeDuplicateEntries(List<FirewallLogEntryInput> entries)
        {
            Dictionary<string, FirewallLogEntryInput> mergedEntries = new();
            foreach (FirewallLogEntryInput entry in entries)
            {
                string flowKey = BuildFlowKey(entry);
                if (mergedEntries.TryGetValue(flowKey, out FirewallLogEntryInput? mergedEntry))
                {
                    MergeIntoEntry(mergedEntry, entry);
                }
                else
                {
                    mergedEntries.Add(flowKey, entry);
                }
            }
            return mergedEntries.Values.ToList();
        }

        private async Task SaveEntries(List<LogDataImportEntry> sourceEntries, string sourcePath)
        {
            DateTimeOffset importTime = DateTimeOffset.UtcNow;
            List<FirewallLogEntryInput> entries = NormalizeEntries(sourceEntries, globalConfig.ImportLogDataMaxEntries, importTime);
            int discardedEntries = Math.Max(0, sourceEntries.Count - entries.Count);
            await ResolveOwners(entries, sourceEntries);
            int entriesBeforeMerge = entries.Count;
            entries = MergeDuplicateEntries(entries);
            int mergedEntries = entriesBeforeMerge - entries.Count;
            if (entries.Count == 0)
            {
                await AddLogEntry(GlobalConst.kImportLogData, 1, LevelFile, $"No valid log entries found in {sourcePath}.json.");
                return;
            }

            long controlId = await CreateImportControl();
            try
            {
                await apiConnection.SendQueryAsync<object>(LogDataQueries.insertLogEntries, new { entries });
                await CompleteImport(controlId, true);
            }
            catch
            {
                await CompleteImport(controlId, false);
                throw;
            }

            string message = $"Imported {entries.Count} log entries from {sourcePath}.json";
            if (discardedEntries > 0)
            {
                message += $"; discarded {discardedEntries} entries below the configured limit.";
            }
            if (mergedEntries > 0)
            {
                message += $"; merged {mergedEntries} repeated entries of the same flow.";
            }
            Log.WriteInfo(LogMessageTitle, message);
            await AddLogEntry(GlobalConst.kImportLogData, 0, LevelFile, message);
        }

        private async Task ResolveOwners(List<FirewallLogEntryInput> entries, List<LogDataImportEntry> sourceEntries)
        {
            Dictionary<string, int?> ownerIds = new(StringComparer.OrdinalIgnoreCase);
            List<FirewallLogEntryInput> unresolvedEntries = new();
            for (int index = 0; index < entries.Count; index++)
            {
                string appId = sourceEntries
                    .OrderByDescending(entry => entry.LogCount)
                    .ElementAt(index)
                    .AppId
                    .Trim();
                int? ownerId = await FindOwnerId(appId, ownerIds);
                if (ownerId is null)
                {
                    Log.WriteWarning(LogMessageTitle, $"Ignoring log data with unknown application id '{appId}'.");
                    unresolvedEntries.Add(entries[index]);
                }
                else
                {
                    entries[index].OwnerId = ownerId.Value;
                }
            }
            entries.RemoveAll(entry => unresolvedEntries.Contains(entry));
        }

        private async Task<int?> FindOwnerId(string appId, Dictionary<string, int?> ownerIds)
        {
            if (ownerIds.TryGetValue(appId, out int? ownerId))
            {
                return ownerId;
            }

            List<OwnerIdModel> owners = await apiConnection.SendQueryAsync<List<OwnerIdModel>>(
                OwnerQueries.getOwnerId,
                new { externalAppId = appId });
            ownerId = owners.FirstOrDefault()?.Id;
            ownerIds[appId] = ownerId;
            return ownerId;
        }

        private async Task<long> CreateImportControl()
        {
            InsertImportControl result = await apiConnection.SendQueryAsync<InsertImportControl>(
                ImportQueries.addImportForLog,
                new { importTypeId = ImportType.LOG });
            return result.Returning.FirstOrDefault()?.ControlId
                ?? throw new InvalidOperationException("Failed to create a log import control record.");
        }

        private async Task CompleteImport(long controlId, bool successful)
        {
            await apiConnection.SendQueryAsync<object>(ImportQueries.completeLogImport, new
            {
                controlId,
                stopTime = DateTime.UtcNow,
                successful
            });
        }

        private async Task DeleteExpiredEntries()
        {
            int retentionDays = Math.Max(0, globalConfig.LogDataRetentionDays);
            DateTimeOffset expiryTime = DateTimeOffset.UtcNow.AddDays(-retentionDays);
            await apiConnection.SendQueryAsync<object>(LogDataQueries.deleteExpiredLogEntries, new { expiryTime });
        }

        private void AcknowledgeImport(string scriptPath, List<string> importFiles)
        {
            if (importFiles.Contains(scriptPath))
            {
                string acknowledgement = string.Join(" ", globalConfig.ImportLogDataScriptArgs, "--acknowledge-import").Trim();
                RunImportScript(scriptPath, acknowledgement);
            }
        }

        private static FirewallLogEntryInput NormalizeEntry(LogDataImportEntry entry, DateTimeOffset importTime)
        {
            if (string.IsNullOrWhiteSpace(entry.AppId) || entry.LogCount < 1)
            {
                throw new InvalidDataException("Log entries require a non-empty app_id and a positive log_count.");
            }

            ValidateService(entry.Protocol, entry.Port);
            return new FirewallLogEntryInput
            {
                LogCount = entry.LogCount,
                Source = ToSingleIpCidr(entry.Source),
                Destination = ToSingleIpCidr(entry.Destination),
                ServiceProtocol = entry.Protocol,
                ServicePort = entry.Port,
                Allowed = ParseAction(entry.Action),
                LogTime = entry.LogTime ?? importTime,
                LoggingRuleName = NormalizeRuleName(entry.RuleName)
            };
        }

        private static string BuildFlowKey(FirewallLogEntryInput entry)
        {
            return string.Join('|', entry.OwnerId, entry.Source, entry.Destination, entry.ServiceProtocol, entry.ServicePort);
        }

        private static void MergeIntoEntry(FirewallLogEntryInput mergedEntry, FirewallLogEntryInput entry)
        {
            mergedEntry.LogCount = (int)Math.Min(int.MaxValue, (long)mergedEntry.LogCount + entry.LogCount);
            if (entry.LogTime >= mergedEntry.LogTime)
            {
                mergedEntry.LogTime = entry.LogTime;
                mergedEntry.Allowed = entry.Allowed;
                mergedEntry.LoggingRuleName = entry.LoggingRuleName;
            }
        }

        private static string ToSingleIpCidr(string value)
        {
            if (!IPAddress.TryParse(value, out IPAddress? address) || value.Contains('/'))
            {
                throw new InvalidDataException($"'{value}' is not a single IP address.");
            }
            return $"{address}/{(address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128)}";
        }

        private static string? NormalizeRuleName(string? ruleName)
        {
            if (string.IsNullOrWhiteSpace(ruleName))
            {
                return null;
            }
            string trimmedRuleName = ruleName.Trim();
            return trimmedRuleName[..Math.Min(trimmedRuleName.Length, 100)];
        }

        private static void ValidateService(int? protocol, int? port)
        {
            if (protocol is < 0 or > 255 || port is < 1 or > GlobalConst.kMaxPortNumber)
            {
                throw new InvalidDataException("Protocol or port is outside its allowed range.");
            }
            if (port.HasValue && protocol is not TcpProtocol and not UdpProtocol)
            {
                throw new InvalidDataException("A port may only be provided for TCP or UDP.");
            }
        }

        private static bool ParseAction(string? action)
        {
            return action?.Trim().ToLowerInvariant() switch
            {
                null or "" or "accept" or "allow" or "allowed" => true,
                "deny" or "drop" or "reject" => false,
                _ => throw new InvalidDataException($"Unsupported log action '{action}'.")
            };
        }
    }
}
