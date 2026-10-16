using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Logging;
using NetTools;
using System.Net;
using System.Text.Json;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Imports normalized logging data produced by customization scripts.
    /// </summary>
    public class LogDataImport(ApiConnection apiConnection, GlobalConfig globalConfig,
        Func<IPAddress, CancellationToken, Task<string?>>? reverseDnsLookup = null) : DataImportBase(apiConnection, globalConfig)
    {
        private const string LogMessageTitle = "Import Log Data";
        private const string LevelFile = "Import File";
        private const int TcpProtocol = 6;
        private const int UdpProtocol = 17;
        private const int LoggedEntriesPerMessage = 50;
        // a source dated wrongly as a whole would otherwise write one warning per entry
        private const int MaxReportedEntriesOutsideTimeRange = 50;
        // An unresolvable address only answers after the resolver timed out, so the lookups of a
        // batch overlap. The bound keeps the import from opening thousands of sockets at once.
        internal const int ReverseLookupParallelism = 16;
        // returns null if the DNS server gave no definitive answer, so the address is looked up again later
        private readonly Func<IPAddress, CancellationToken, Task<string?>> reverseDnsLookup = reverseDnsLookup
            ?? (async (address, token) => SelectDnsName(await IpOperations.TryDnsReverseLookUpAllAsync(address, token)));

        /// <summary>
        /// Selects the first PTR name directly from the indexable DNS result collection.
        /// </summary>
        /// <returns>The first name, an empty string for an address without name, or null for a failed lookup.</returns>
        internal static string? SelectDnsName(IReadOnlyList<string>? names)
        {
            if (names is null)
            {
                return null;
            }
            return names.Count == 0 ? "" : names[0] ?? "";
        }

        /// <summary>
        /// Runs configured log data imports and removes expired entries.
        /// </summary>
        /// <param name="cancellationToken">Stops during preparation; a database write is completed and acknowledged.</param>
        /// <returns>Sources which could not be imported.</returns>
        public async Task<List<string>> Run(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<string> sources = JsonSerializer.Deserialize<List<string>>(globalConfig.ImportLogDataPath)
                ?? throw new JsonException("Log data import sources could not be deserialized.");
            List<string> failedImports = new();

            foreach (string source in sources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ImportSource(source, failedImports, cancellationToken);
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
        /// Keeps the entries with the highest log counts for every owner. Applied after the flows
        /// of the import were merged, so a flow reported several times is ranked by its total
        /// count and not by its largest single row. Limiting each owner independently prevents a
        /// loud application from displacing all current flows of a quieter application in the
        /// same source.
        /// </summary>
        public static List<FirewallLogEntryInput> LimitEntries(List<FirewallLogEntryInput> entries, int maxEntries)
        {
            return entries
                .GroupBy(entry => entry.OwnerId)
                .SelectMany(ownerEntries => ownerEntries
                    .OrderByDescending(entry => entry.LogCount)
                    .Take(Math.Max(0, maxEntries)))
                .OrderByDescending(entry => entry.LogCount)
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

        private async Task ImportSource(string configuredSource, List<string> failedImports, CancellationToken cancellationToken)
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
                int logTimeRangeInSeconds = ResolveLogTimeRange(importFileData.LogTimeRangeInSeconds, globalConfig.DefaultLogTimeRangeInSeconds);
                // Preparation can still be cancelled; writes must finish before acknowledgement.
                cancellationToken.ThrowIfCancellationRequested();
                DateTimeOffset importTime = importFileData.ImportTime ?? DateTimeOffset.UtcNow;
                WarnAboutEntriesOutsideTimeRange(importFileData.Logs, importTime, logTimeRangeInSeconds, sourcePath);
                await SaveEntries(importFileData.Logs, sourcePath, importTime, logTimeRangeInSeconds, cancellationToken);
                await AcknowledgeImport(scriptPath, importFiles, sourcePath);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
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
        /// Determines the period the log counts of an import file were aggregated over. A period named
        /// by the file overrides the configured default, which applies to files without one - like those
        /// converted from CSV data, which cannot name it. A file naming a period which is not positive
        /// is rejected as a whole and kept for the next run, the log table would present its counts as
        /// traffic of a period which cannot exist.
        /// </summary>
        /// <returns>The period in seconds.</returns>
        public static int ResolveLogTimeRange(int? fileLogTimeRangeInSeconds, int defaultLogTimeRangeInSeconds)
        {
            if (fileLogTimeRangeInSeconds is null)
            {
                return Math.Max(1, defaultLogTimeRangeInSeconds);
            }
            if (fileLogTimeRangeInSeconds < 1)
            {
                throw new InvalidDataException("The top level log_time_range_in_seconds of a log data file must be positive.");
            }
            return fileLogTimeRangeInSeconds.Value;
        }

        /// <summary>
        /// Finds the entries whose log time lies outside the period the file was aggregated over, which
        /// ends at the import time and reaches back by the log time range. An entry without log time is
        /// stamped with the import time and therefore never outside.
        /// </summary>
        /// <returns>The entries logged before the period started or after the import.</returns>
        public static List<LogDataImportEntry> FindEntriesOutsideTimeRange(IEnumerable<LogDataImportEntry> entries,
            DateTimeOffset importTime, int logTimeRangeInSeconds)
        {
            DateTimeOffset rangeStart = importTime.AddSeconds(-logTimeRangeInSeconds);
            return entries
                .Where(entry => entry.LogTime is not null && (entry.LogTime < rangeStart || entry.LogTime > importTime))
                .ToList();
        }

        /// <summary>
        /// Warns about every entry logged outside the period the file names (or the default period), at
        /// most <see cref="MaxReportedEntriesOutsideTimeRange"/> of them. The entries are imported
        /// anyway: their counts belong to the flow, only the period in the title of the log table does
        /// not describe them correctly, which points at a wrong log time range or a stale export.
        /// </summary>
        private static void WarnAboutEntriesOutsideTimeRange(List<LogDataImportEntry> entries, DateTimeOffset importTime,
            int logTimeRangeInSeconds, string sourcePath)
        {
            List<LogDataImportEntry> outsideEntries = FindEntriesOutsideTimeRange(entries, importTime, logTimeRangeInSeconds);
            if (outsideEntries.Count == 0)
            {
                return;
            }
            DateTimeOffset rangeStart = importTime.AddSeconds(-logTimeRangeInSeconds);
            foreach (LogDataImportEntry entry in outsideEntries.Take(MaxReportedEntriesOutsideTimeRange))
            {
                Log.WriteWarning(LogMessageTitle, $"Log entry of application '{entry.AppId}' ({entry.Source} -> {entry.Destination})" +
                    $" in {sourcePath}.json was logged at {entry.LogTime:O}, outside the expected log time range" +
                    $" {rangeStart:O} - {importTime:O}.");
            }
            Log.WriteWarning(LogMessageTitle, $"{outsideEntries.Count} log entries of {sourcePath}.json were logged outside the expected" +
                $" log time range of {logTimeRangeInSeconds} seconds before {importTime:O}" +
                (outsideEntries.Count > MaxReportedEntriesOutsideTimeRange ? $", the first {MaxReportedEntriesOutsideTimeRange} are listed above." : "."));
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
        /// Imports the entries of one source file. A source is always consumed, also when none of
        /// its entries could be imported: keeping it back would repeat the same rejected import in
        /// every run and, with replaceExistingLogData, would delete the rows of its applications
        /// again and again - including rows another source imported for the same application in
        /// the meantime. What could not be imported is written to the log by
        /// <see cref="LogUnimportedEntries"/> before the source is removed.
        /// </summary>
        private async Task SaveEntries(List<LogDataImportEntry> sourceEntries, string sourcePath, DateTimeOffset importTime,
            int logTimeRangeInSeconds, CancellationToken cancellationToken)
        {
            List<NormalizedLogEntry> normalizedEntries = NormalizeValidEntries(sourceEntries, importTime, globalConfig.AllowLogDataPortWithoutProtocol);
            int invalidEntries = Math.Max(0, sourceEntries.Count - normalizedEntries.Count);
            Dictionary<string, int?> ownerIdsByAppId = new(StringComparer.Ordinal);
            List<int> sourceOwnerIds = globalConfig.ReplaceExistingLogData
                ? await ResolveSourceOwnerIds(sourceEntries, ownerIdsByAppId)
                : [];
            List<FirewallLogEntryInput> resolvedEntries = await ResolveOwners(normalizedEntries, ownerIdsByAppId);
            int unresolvedEntries = normalizedEntries.Count - resolvedEntries.Count;
            // merge before limiting, so a flow reported several times is ranked by its total
            List<FirewallLogEntryInput> mergedFlows = MergeDuplicateEntries(resolvedEntries);
            int mergedEntries = resolvedEntries.Count - mergedFlows.Count;
            List<FirewallLogEntryInput> entries = LimitEntries(mergedFlows, globalConfig.ImportLogDataMaxEntries);
            int discardedEntries = mergedFlows.Count - entries.Count;
            WarnAboutDroppedEntries(sourcePath, invalidEntries, unresolvedEntries, discardedEntries);
            if (entries.Count == 0)
            {
                LogUnimportedEntries(sourceEntries, sourcePath, globalConfig.ImportLogDataMaxEntries);
                cancellationToken.ThrowIfCancellationRequested();
                await RemoveReplacedEntries(sourceOwnerIds, sourcePath, sourceEntries.Count);
                return;
            }

            foreach (FirewallLogEntryInput entry in entries)
            {
                entry.ImportTime = importTime;
                entry.LogTimeRangeInSeconds = logTimeRangeInSeconds;
            }
            List<IpMetadata> metadata = await BuildIpMetadata(entries, cancellationToken);
            // Once writing starts it must complete, including acknowledgement of the source.
            cancellationToken.ThrowIfCancellationRequested();
            await RunAsImport(() => WriteEntries(entries, metadata, sourceOwnerIds));

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

        /// <summary>
        /// Writes the entries of a source which delivered nothing importable to the log, so the
        /// data survives the removal of the source file and can be looked up after the setting
        /// which rejected it was corrected. Why every single entry was rejected is logged before
        /// by NormalizeValidEntries or ResolveOwners; this is the data itself.
        /// The entries are written in batches, one message per batch, so a large export does not
        /// end up as a single unreadable log line, and at most importLogDataMaxEntries of them:
        /// a successful import would not have kept more either, so a huge export of an application
        /// nobody knows cannot flood the middleware log in every import run.
        /// </summary>
        private static void LogUnimportedEntries(List<LogDataImportEntry> sourceEntries, string sourcePath, int maxLoggedEntries)
        {
            if (sourceEntries.Count == 0)
            {
                return;
            }

            int loggedEntries = Math.Clamp(maxLoggedEntries, 0, sourceEntries.Count);
            string reference = loggedEntries < sourceEntries.Count
                ? $" The source is removed anyway, its first {loggedEntries} entries are written to the log below for" +
                  $" future reference ({nameof(GlobalConfig.ImportLogDataMaxEntries)})."
                : " The source is removed anyway, its entries are written to the log below for future reference.";
            Log.WriteWarning(LogMessageTitle, $"None of the {sourceEntries.Count} entries of {sourcePath}.json could be imported." + reference);
            for (int firstEntry = 0; firstEntry < loggedEntries; firstEntry += LoggedEntriesPerMessage)
            {
                List<LogDataImportEntry> batch = sourceEntries.GetRange(firstEntry,
                    Math.Min(LoggedEntriesPerMessage, loggedEntries - firstEntry));
                Log.WriteWarning(LogMessageTitle, $"Not imported entries of {sourcePath}.json" +
                    $" ({firstEntry + 1} - {firstEntry + batch.Count}): {JsonSerializer.Serialize(batch)}");
            }
        }

        /// <summary>
        /// Removes the stored rows of the applications of a source which delivered nothing
        /// importable, keeping the promise of <see cref="ResolveSourceOwnerIds"/> that a source is
        /// the current truth about the applications it reports. Without this the rows of an earlier
        /// period would stay on display as if they were current. Outside replaceExistingLogData
        /// mode there are no owners to replace and nothing is removed.
        /// </summary>
        private async Task RemoveReplacedEntries(List<int> sourceOwnerIds, string sourcePath, int sourceEntryCount)
        {
            string message = sourceEntryCount == 0
                ? $"No log entries found in {sourcePath}.json."
                : $"No valid log entries found in {sourcePath}.json, its {sourceEntryCount} entries are written to the log.";
            if (sourceOwnerIds.Count > 0)
            {
                await RunAsImport(() => apiConnection.SendQueryAsync<object>(LogDataQueries.deleteLogEntriesOfOwners,
                    new { ownerIds = sourceOwnerIds }));
                message += " The stored log data of the applications it reports was removed.";
            }
            await AddLogEntry(GlobalConst.kImportLogData, 1, LevelFile, message);
        }

        /// <summary>
        /// Runs a change of the stored log data inside an import control record, so a failed change
        /// is visible as a failed import instead of leaving an import which never ended.
        /// </summary>
        private async Task RunAsImport(Func<Task> changeLogData)
        {
            long controlId = await CreateImportControl();
            try
            {
                await changeLogData();
                await CompleteImport(controlId, true);
            }
            catch
            {
                await CompleteImport(controlId, false);
                throw;
            }
        }

        /// <summary>
        /// Replaces all stored rows of applications named in the source when configured. The
        /// delete and insert fields share one GraphQL mutation so Hasura executes them in one
        /// transaction and a failed insert cannot leave the applications without their old rows.
        /// The replacement is scoped to one source on purpose: replaceExistingLogData means that
        /// every source owns the applications it reports, so configuring two sources for the same
        /// application is a misconfiguration - the source imported later replaces the rows of the
        /// one imported before instead of adding to them, also within the same run.
        /// </summary>
        private async Task WriteEntries(List<FirewallLogEntryInput> entries, List<IpMetadata> metadata, List<int> sourceOwnerIds)
        {
            if (globalConfig.ReplaceExistingLogData)
            {
                await apiConnection.SendQueryAsync<object>(LogDataQueries.replaceLogEntries, new { ownerIds = sourceOwnerIds, entries, metadata });
                return;
            }
            await apiConnection.SendQueryAsync<object>(LogDataQueries.insertLogEntries, new { entries, metadata });
        }

        /// <summary>
        /// Calculates application, network-area and DNS information once for every address kept
        /// by this import batch. Empty values are persisted as well, so the UI can distinguish a
        /// completed lookup without a result from a missing metadata row.
        /// The address ranges are read once and matched in memory, see
        /// <see cref="LogDataQueries.getIpMetadataSources"/>, and the reverse lookups of the batch
        /// run with a bounded parallelism, because an address without a PTR record only answers
        /// after the resolver timed out and a batch holds thousands of addresses.
        /// </summary>
        private async Task<List<IpMetadata>> BuildIpMetadata(List<FirewallLogEntryInput> entries, CancellationToken cancellationToken)
        {
            List<string> addresses = entries
                .SelectMany(entry => new List<string> { entry.Source, entry.Destination })
                .Distinct(StringComparer.Ordinal)
                .ToList();
            List<IpMetadataSource> allSources = await apiConnection.SendQueryAsync<List<IpMetadataSource>>(
                LogDataQueries.getIpMetadataSources);
            List<PreparedMetadataSource> preparedSources = PrepareMetadataSources(allSources);
            List<IpMetadata> storedMetadata = await apiConnection.SendQueryAsync<List<IpMetadata>>(
                LogDataQueries.getIpMetadata, new { addresses });
            Dictionary<string, IpMetadata> storedByAddress = storedMetadata.ToDictionary(item => item.IpAddress, StringComparer.Ordinal);
            // the setting is read once, the config subscription may change it while the lookups run
            ReverseLookupBatch lookupBatch = new(globalConfig.ResolveLogDataDns);
            IpMetadata[] metadata = new IpMetadata[addresses.Count];
            await Parallel.ForAsync(0, addresses.Count,
                new ParallelOptions { MaxDegreeOfParallelism = ReverseLookupParallelism, CancellationToken = cancellationToken },
                async (index, token) => metadata[index] = await BuildAddressMetadata(addresses[index], preparedSources,
                    storedByAddress, lookupBatch, token));
            WarnAboutFailedDnsLookups(metadata, lookupBatch);
            return [.. metadata];
        }

        /// <summary>
        /// With lookups enabled, an address is left without a completed lookup only when its lookup failed
        /// or was skipped because no DNS server could be reached. Such addresses are looked up again by the
        /// next import, so a DNS outage does not leave them without name for good.
        /// </summary>
        private static void WarnAboutFailedDnsLookups(IpMetadata[] metadata, ReverseLookupBatch lookupBatch)
        {
            int failedLookups = metadata.Count(item => !item.DnsLookupCompleted);
            if (!lookupBatch.Enabled || failedLookups == 0)
            {
                return;
            }
            string reason = lookupBatch.ResolverUnreachable
                ? "no DNS server could be reached, so the remaining lookups of this import were skipped"
                : "the DNS server gave no definitive answer";
            Log.WriteWarning(LogMessageTitle, $"Reverse-DNS lookup of {failedLookups} of {metadata.Length} addresses did not complete" +
                $" ({reason}), they are looked up again by the next import. If no DNS server is reachable from the middleware," +
                " disable the setting 'Resolve reverse DNS for log data' under Settings - Logging.");
        }

        /// <summary>
        /// Converts the address ranges into a form which can be matched against a logged address
        /// without parsing them again for every address. A range which cannot be parsed is skipped
        /// instead of failing the import; it would contribute no metadata in any case.
        /// </summary>
        private static List<PreparedMetadataSource> PrepareMetadataSources(List<IpMetadataSource> sources)
        {
            List<PreparedMetadataSource> preparedSources = [];
            foreach (IpMetadataSource source in sources)
            {
                if (!IPAddress.TryParse(source.Ip.StripOffNetmask(), out IPAddress? begin)
                    || !IPAddress.TryParse(source.IpEnd.StripOffNetmask(), out IPAddress? end)
                    || begin.AddressFamily != end.AddressFamily)
                {
                    Log.WriteWarning(LogMessageTitle, $"Ignoring address range '{source.Ip}-{source.IpEnd}' which is not a valid IP range.");
                    continue;
                }
                preparedSources.Add(new PreparedMetadataSource(new IPAddressRange(begin, end), source));
            }
            return preparedSources;
        }

        /// <summary>
        /// Collects the applications and network areas of every range containing the address and
        /// resolves its name. The values are sorted, so repeated imports of the same address write
        /// the same row and the display order does not depend on the order of the ranges.
        /// </summary>
        private async Task<IpMetadata> BuildAddressMetadata(string address, List<PreparedMetadataSource> preparedSources,
            Dictionary<string, IpMetadata> storedByAddress, ReverseLookupBatch lookupBatch, CancellationToken cancellationToken)
        {
            IPAddress ipAddress = IPAddress.Parse(address.StripOffNetmask());
            IPAddressRange addressRange = new(ipAddress, ipAddress);
            List<IpMetadataSource> matchingSources = preparedSources
                .Where(source => IpOperations.RangeOverlapExists(source.Range, addressRange))
                .Select(source => source.Source)
                .ToList();
            storedByAddress.TryGetValue(address, out IpMetadata? stored);
            bool lookupCompleted = stored?.DnsLookupCompleted == true;
            string dns = stored?.Dns ?? "";
            if (!lookupCompleted && lookupBatch.ShouldLookUp)
            {
                string? resolvedDns = await ResolveDns(ipAddress, lookupBatch, cancellationToken);
                if (resolvedDns is not null)
                {
                    dns = resolvedDns;
                    lookupCompleted = true;
                }
            }
            return new IpMetadata
            {
                IpAddress = address,
                AppIds = SortedDistinctValues(matchingSources.Select(source => source.Owner?.ExtAppId)),
                AreaIds = SortedDistinctValues(matchingSources
                    .SelectMany(source => source.AreaMemberships)
                    .Select(membership => membership.Area?.IdString)),
                Dns = dns,
                DnsLookupCompleted = lookupCompleted
            };
        }

        /// <summary>
        /// Resolves PTR records asynchronously. DNS failures return null, so the lookup is not marked as
        /// completed and is repeated by the next import, while shutdown cancellation leaves the source
        /// available for retry without changing stored log data. A lookup which throws reached no DNS
        /// server, so the batch skips its remaining lookups instead of waiting for the same timeout again.
        /// </summary>
        /// <returns>The name, an empty string for an address without name, or null if the lookup failed.</returns>
        private async Task<string?> ResolveDns(IPAddress address, ReverseLookupBatch lookupBatch, CancellationToken cancellationToken)
        {
            try
            {
                return await reverseDnsLookup(address, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Log.WriteDebug(LogMessageTitle, $"Reverse-DNS lookup of {address} failed: {exception.Message}");
                lookupBatch.MarkResolverUnreachable();
                return null;
            }
        }

        private static List<string> SortedDistinctValues(IEnumerable<string?> values)
        {
            return values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// An address range of <see cref="LogDataQueries.getIpMetadataSources"/> with its parsed bounds.
        /// </summary>
        private sealed record PreparedMetadataSource(IPAddressRange Range, IpMetadataSource Source);

        /// <summary>
        /// Reverse lookups of one import batch. Once a lookup found no DNS server answering, the remaining
        /// addresses of the batch are not looked up, because each of them would only wait for the same
        /// timeout. They stay without completed lookup and are looked up again by the next import.
        /// </summary>
        private sealed class ReverseLookupBatch(bool enabled)
        {
            private volatile bool resolverUnreachable;

            /// <summary>
            /// Whether reverse lookups are enabled for this batch.
            /// </summary>
            public bool Enabled { get; } = enabled;

            /// <summary>
            /// Whether a lookup of this batch found no DNS server answering.
            /// </summary>
            public bool ResolverUnreachable => resolverUnreachable;

            /// <summary>
            /// Whether a further address of this batch is to be looked up.
            /// </summary>
            public bool ShouldLookUp => Enabled && !resolverUnreachable;

            /// <summary>
            /// Stops the further lookups of this batch.
            /// </summary>
            public void MarkResolverUnreachable()
            {
                resolverUnreachable = true;
            }
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

        /// <summary>
        /// Collects the owners of all applications named in the source, used to replace their
        /// stored rows in <see cref="WriteEntries"/>.
        /// The raw source entries are used deliberately, not the entries which survive validation,
        /// the owner lookup and the importLogDataMaxEntries limit: a source reporting an
        /// application is the current truth about that application, so its stored rows are dropped
        /// even when none of its new entries can be imported (see <see cref="RemoveReplacedEntries"/>).
        /// The application then shows no log data instead of data from an earlier period, and the
        /// dropped entries are reported by WarnAboutDroppedEntries.
        /// </summary>
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
            // after the expired entries, so the metadata of an address which just lost its last
            // entry is removed in the same run instead of surviving until the next import
            await apiConnection.SendQueryAsync<object>(LogDataQueries.deleteOrphanedIpMetadata);
        }

        /// <summary>
        /// Lets the import script acknowledge the processed source files, which deletes them.
        /// The whole source is acknowledged on purpose, also when entries were dropped by the
        /// importLogDataMaxEntries limit, by an unknown application id or because they could not
        /// be converted, and also when nothing of it could be imported at all: an import run is
        /// expected to consume its source completely, otherwise the same rejected entries would be
        /// read again in every following run. WarnAboutDroppedEntries reports how many entries are
        /// lost with the deleted file, LogUnimportedEntries writes the entries of a source which
        /// delivered nothing importable to the log before it is removed.
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

        /// <summary>
        /// Builds the key identifying one logged flow. It contains the same fields as the
        /// log_entry_unique_flow constraint of the database, including whether the flow was
        /// allowed: an accepted and a blocked flow between the same peers are two different
        /// results, so merging them would hide one of them behind the count of the other.
        /// </summary>
        private static string BuildFlowKey(FirewallLogEntryInput entry)
        {
            return string.Join('|', entry.OwnerId, entry.Source, entry.Destination, entry.ServiceProtocol, entry.ServicePort, entry.Allowed);
        }

        private static void MergeIntoEntry(FirewallLogEntryInput mergedEntry, FirewallLogEntryInput entry)
        {
            mergedEntry.LogCount = (int)Math.Min(int.MaxValue, (long)mergedEntry.LogCount + entry.LogCount);
            if (entry.LogTime >= mergedEntry.LogTime)
            {
                // merged entries share the flow key, so only the fields outside it can differ
                mergedEntry.LogTime = entry.LogTime;
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
