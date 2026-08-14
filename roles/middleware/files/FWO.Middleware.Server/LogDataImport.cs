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
        /// Entries which cannot be converted are skipped, see <see cref="NormalizeValidEntries"/>.
        /// </summary>
        public static List<FirewallLogEntryInput> NormalizeEntries(IEnumerable<LogDataImportEntry> entries, DateTimeOffset importTime,
            bool allowPortWithoutProtocol = false)
        {
            return NormalizeValidEntries(entries, importTime, allowPortWithoutProtocol).Select(entry => entry.Entry).ToList();
        }

        /// <summary>
        /// Keeps the entries with the highest log counts. Applied after the flows of the import
        /// were merged, so a flow reported by several source files is ranked by its total count
        /// and not by its largest single row.
        /// </summary>
        public static List<FirewallLogEntryInput> LimitEntries(List<FirewallLogEntryInput> entries, int maxEntries)
        {
            return entries
                .OrderByDescending(entry => entry.LogCount)
                .Take(Math.Max(0, maxEntries))
                .ToList();
        }

        /// <summary>
        /// Converts the external log data and keeps the application id of every entry.
        /// An entry which cannot be converted is logged and skipped instead of failing the whole
        /// source file: the file is acknowledged and deleted afterwards, so an entry which stops
        /// the import would otherwise block its source in every following import run.
        /// </summary>
        /// <returns>The convertible entries of the source.</returns>
        private static List<NormalizedLogEntry> NormalizeValidEntries(IEnumerable<LogDataImportEntry> entries, DateTimeOffset importTime,
            bool allowPortWithoutProtocol)
        {
            List<NormalizedLogEntry> normalizedEntries = new();
            foreach (LogDataImportEntry entry in entries)
            {
                try
                {
                    normalizedEntries.Add(new NormalizedLogEntry(NormalizeEntry(entry, importTime, allowPortWithoutProtocol), entry.AppId.Trim()));
                }
                catch (InvalidDataException exception)
                {
                    Log.WriteWarning(LogMessageTitle, $"Ignoring invalid log entry of application '{entry.AppId}': {exception.Message}");
                }
            }
            return normalizedEntries;
        }

        private async Task ImportSource(string configuredSource, List<string> failedImports)
        {
            string sourcePath = ImportPathPolicy.RemoveAllowedExtension(configuredSource);
            try
            {
                List<string> importFiles = ValidateConfiguredImportSource(sourcePath);
                string scriptPath = sourcePath + ".py";
                if (importFiles.Contains(scriptPath) && !RunImportScript(scriptPath, globalConfig.ImportLogDataScriptArgs))
                {
                    throw new InvalidOperationException($"Log data import script {scriptPath} failed.");
                }

                ReadFile(sourcePath + ".json");
                LogDataImportFile importFileData = JsonSerializer.Deserialize<LogDataImportFile>(importFile)
                    ?? throw new JsonException("Log data file could not be parsed.");
                if (await SaveEntries(importFileData.Logs, sourcePath, importFileData.ImportTime ?? DateTimeOffset.UtcNow))
                {
                    await AcknowledgeImport(scriptPath, importFiles, sourcePath);
                }
                else
                {
                    string message = $"No entry of {sourcePath}.json could be imported, the source files are kept."
                        + " Check the log data settings and the reported entries before the next run.";
                    Log.WriteWarning(LogMessageTitle, message);
                    await AddLogEntry(GlobalConst.kImportLogData, 1, LevelFile, message);
                }
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
        /// must not contain the same flow twice. The log counts are added up within a batch, while
        /// a later import replaces the stored count of a flow (see the on-conflict clause of
        /// insertLogEntries): a stored count therefore describes the last imported period of a
        /// flow, not the total since it was first seen.
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

        /// <summary>
        /// Imports the entries of one source file.
        /// </summary>
        /// <returns>
        /// False if the source contained entries but none of them could be imported. Such a source
        /// is kept instead of being acknowledged: losing a whole export because of a configuration
        /// which rejects all of its entries cannot be undone, while a source kept back is reported
        /// in every run and can be imported after the configuration was corrected.
        /// </returns>
        private async Task<bool> SaveEntries(List<LogDataImportEntry> sourceEntries, string sourcePath, DateTimeOffset importTime)
        {
            List<NormalizedLogEntry> normalizedEntries = NormalizeValidEntries(sourceEntries, importTime, globalConfig.AllowLogDataPortWithoutProtocol);
            int invalidEntries = Math.Max(0, sourceEntries.Count - normalizedEntries.Count);
            Dictionary<string, int?> ownerIdsByAppId = new(StringComparer.Ordinal);
            List<int> sourceOwnerIds = globalConfig.ReplaceExistingLogData
                ? await ResolveSourceOwnerIds(sourceEntries, ownerIdsByAppId)
                : [];
            List<FirewallLogEntryInput> resolvedEntries = await ResolveOwners(normalizedEntries, ownerIdsByAppId);
            int unresolvedEntries = normalizedEntries.Count - resolvedEntries.Count;
            // merge before limiting, so a flow reported by several source files is ranked by its total
            List<FirewallLogEntryInput> mergedFlows = MergeDuplicateEntries(resolvedEntries);
            int mergedEntries = resolvedEntries.Count - mergedFlows.Count;
            List<FirewallLogEntryInput> entries = LimitEntries(mergedFlows, globalConfig.ImportLogDataMaxEntries);
            int discardedEntries = mergedFlows.Count - entries.Count;
            WarnAboutDroppedEntries(sourcePath, invalidEntries, unresolvedEntries, discardedEntries);
            if (entries.Count == 0)
            {
                await AddLogEntry(GlobalConst.kImportLogData, 1, LevelFile, $"No valid log entries found in {sourcePath}.json.");
                return sourceEntries.Count == 0;
            }

            long controlId = await CreateImportControl();
            try
            {
                await WriteEntries(entries, sourceOwnerIds);
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
            return true;
        }

        /// <summary>
        /// Replaces all stored rows of applications named in the source when configured. The
        /// delete and insert fields share one GraphQL mutation so Hasura executes them in one
        /// transaction and a failed insert cannot leave the applications without their old rows.
        /// </summary>
        private async Task WriteEntries(List<FirewallLogEntryInput> entries, List<int> sourceOwnerIds)
        {
            if (globalConfig.ReplaceExistingLogData)
            {
                await apiConnection.SendQueryAsync<object>(LogDataQueries.replaceLogEntries, new { ownerIds = sourceOwnerIds, entries });
                return;
            }
            await apiConnection.SendQueryAsync<object>(LogDataQueries.insertLogEntries, new { entries });
        }

        /// <summary>
        /// Warns about the entries which are not imported although their source file is
        /// acknowledged and therefore deleted by the import script. Dropping them is intended:
        /// only the loudest flows up to importLogDataMaxEntries are kept, and log data of an
        /// unknown application cannot be assigned to an owner.
        /// </summary>
        private static void WarnAboutDroppedEntries(string sourcePath, int invalidEntries, int unresolvedEntries, int discardedEntries)
        {
            int droppedEntries = invalidEntries + unresolvedEntries + discardedEntries;
            if (droppedEntries <= 0)
            {
                return;
            }

            Log.WriteWarning(LogMessageTitle, $"{droppedEntries} log entries of {sourcePath}.json are not imported" +
                $" ({invalidEntries} invalid, {unresolvedEntries} without a known application," +
                $" {discardedEntries} above the configured {nameof(GlobalConfig.ImportLogDataMaxEntries)})" +
                $" and are removed with the acknowledged source file.");
        }

        private async Task<List<int>> ResolveSourceOwnerIds(List<LogDataImportEntry> sourceEntries, Dictionary<string, int?> ownerIds)
        {
            List<int> resolvedOwnerIds = [];
            IEnumerable<string> sourceAppIds = sourceEntries
                .Select(entry => entry.AppId?.Trim())
                .Where(appId => !string.IsNullOrWhiteSpace(appId))
                .Cast<string>()
                .Distinct(StringComparer.Ordinal);
            foreach (string appId in sourceAppIds)
            {
                int? ownerId = await FindOwnerId(appId, ownerIds);
                if (ownerId.HasValue)
                {
                    resolvedOwnerIds.Add(ownerId.Value);
                }
            }
            return resolvedOwnerIds.Distinct().ToList();
        }

        private async Task<List<FirewallLogEntryInput>> ResolveOwners(List<NormalizedLogEntry> normalizedEntries,
            Dictionary<string, int?> ownerIds)
        {
            // the owner lookup matches app_id_external case sensitively, so the cache has to
            // distinguish the same spellings, otherwise 'app-1' would inherit the owner of 'APP-1'
            List<FirewallLogEntryInput> resolvedEntries = new();
            foreach (NormalizedLogEntry normalizedEntry in normalizedEntries)
            {
                string appId = normalizedEntry.AppId;
                int? ownerId = await FindOwnerId(appId, ownerIds);
                if (ownerId is null)
                {
                    Log.WriteWarning(LogMessageTitle, $"Ignoring log data with unknown application id '{appId}'.");
                    continue;
                }

                normalizedEntry.Entry.OwnerId = ownerId.Value;
                resolvedEntries.Add(normalizedEntry.Entry);
            }
            return resolvedEntries;
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

        /// <summary>
        /// Removes log entries which are older than logDataRetentionDays.
        /// The age of an entry is the time the traffic was logged (log_time from the source data),
        /// not the time it was imported. This is intended: retention describes how long logged
        /// traffic is kept, independent of when someone happens to export it. An export whose
        /// entries are already older than the retention is therefore imported and removed again
        /// in the same run, and its source files are still acknowledged and deleted, because the
        /// entries are outside the configured retention either way.
        /// </summary>
        private async Task DeleteExpiredEntries()
        {
            int retentionDays = Math.Max(0, globalConfig.LogDataRetentionDays);
            DateTimeOffset expiryTime = DateTimeOffset.UtcNow.AddDays(-retentionDays);
            await apiConnection.SendQueryAsync<object>(LogDataQueries.deleteExpiredLogEntries, new { expiryTime });
        }

        /// <summary>
        /// Lets the import script acknowledge the processed source files, which deletes them.
        /// The whole source is acknowledged on purpose, also when entries were dropped by the
        /// importLogDataMaxEntries limit, by an unknown application id or because they could not
        /// be converted: an import run is expected to consume its source completely, otherwise the
        /// same rejected entries would be read again in every following run. WarnAboutDroppedEntries
        /// reports how many entries are lost with the deleted file.
        /// </summary>
        private async Task AcknowledgeImport(string scriptPath, List<string> importFiles, string sourcePath)
        {
            if (!importFiles.Contains(scriptPath))
            {
                return;
            }

            string acknowledgement = string.Join(" ", globalConfig.ImportLogDataScriptArgs, "--acknowledge-import").Trim();
            if (!RunImportScript(scriptPath, acknowledgement))
            {
                // the entries of this source were imported, only their removal failed, so the
                // source is reported here instead of being handled as a failed import
                string message = $"Acknowledging the imported data of {sourcePath}.json failed." +
                    " The source files are kept and their entries are imported again in the next run.";
                Log.WriteError(LogMessageTitle, message);
                await AddLogEntry(GlobalConst.kImportLogData, 2, LevelFile, message);
            }
        }

        private static FirewallLogEntryInput NormalizeEntry(LogDataImportEntry entry, DateTimeOffset importTime, bool allowPortWithoutProtocol)
        {
            if (string.IsNullOrWhiteSpace(entry.AppId) || entry.LogCount < 1)
            {
                throw new InvalidDataException("Log entries require a non-empty app_id and a positive log_count.");
            }

            ValidateService(entry.Protocol, entry.Port, allowPortWithoutProtocol);
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

            bool isIpV4 = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
            if (!isIpV4 && address.ScopeId != 0)
            {
                // the cidr column does not accept a zone index like fe80::1%3, and the interface
                // the log was written on is not part of the address itself
                address = new IPAddress(address.GetAddressBytes());
            }
            return $"{address}/{(isIpV4 ? 32 : 128)}";
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

        /// <summary>
        /// Checks protocol and port of a logged flow. Log data of some sources carries ports
        /// without naming the protocol, which is why allowLogDataPortWithoutProtocol makes the
        /// requirement of a transport protocol configurable.
        /// </summary>
        private static void ValidateService(int? protocol, int? port, bool allowPortWithoutProtocol)
        {
            if (protocol is < 0 or > 255 || port is < 1 or > GlobalConst.kMaxPortNumber)
            {
                throw new InvalidDataException("Protocol or port is outside its allowed range.");
            }
            if (!port.HasValue)
            {
                return;
            }
            if (protocol is null && allowPortWithoutProtocol)
            {
                return;
            }
            if (protocol is not TcpProtocol and not UdpProtocol)
            {
                throw new InvalidDataException("A port may only be provided for TCP or UDP.");
            }
        }

        /// <summary>
        /// Maps the action of a logged flow to allowed or denied.
        /// Only the known wordings for a blocked flow count as denied, everything else is treated
        /// as allowed: log data uses vendor specific and localized wordings, and dropping an entry
        /// because of an unknown one would lose the flow with the acknowledged source file.
        /// </summary>
        private static bool ParseAction(string? action)
        {
            return action?.Trim().ToLowerInvariant() switch
            {
                "deny" or "drop" or "reject" or "block" or "blocked" or "denied" => false,
                _ => true
            };
        }

        /// <summary>
        /// A converted log entry together with the application id it was imported for.
        /// Keeping the application id on the entry avoids matching the converted entries to their
        /// source entries by position, which does not hold as soon as entries are skipped.
        /// </summary>
        private sealed record NormalizedLogEntry(FirewallLogEntryInput Entry, string AppId);
    }
}
