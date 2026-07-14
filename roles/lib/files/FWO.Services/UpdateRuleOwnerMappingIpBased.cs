using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Logging;
using FWO.Services.EventMediator.Events;
using NetTools;
using System.Net.Sockets;
using System.Net;
using System.Text.Json;

namespace FWO.Services
{
    public class UpdateRuleOwnerMappingIpBased : UpdateRuleOwnerMappingBase
    {
        public override OwnerMappingSourceStm Source => OwnerMappingSourceStm.IpBased;

        public UpdateRuleOwnerMappingIpBased(ApiConnection apiConnection, GlobalConfig globalConfig)
            : base(apiConnection, globalConfig)
        {
        }

        public override async Task<bool> RunAsync(UpdateRuleOwnerMappingEventArgs? eventArgs = null)
        {
            bool isFullReInitialize = eventArgs?.isFullReInitialize ?? false;
            return await UpdateRuleOwners(RunFullReinitialize, RunIncremental, isFullReInitialize);
        }

        /// <summary>
        /// Delegates the full reinitialize flow to the shared base implementation using IP-based rule and owner sources.
        /// </summary>
        private async Task<bool> RunFullReinitialize() => await RunFullReinitialize(RuleQueries.getRulesForOwnerMappingIpBased, () => apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwnersForRuleOwnerIpBased), BuildNewRuleOwnersIpBased);

        /// <summary>
        /// Delegates incremental processing of pending imports to the shared base implementation for IP-based mapping.
        /// </summary>
        private async Task<bool> RunIncremental() => await RunIncremental(ProcessIncrementalImportIpBased, RunFullReinitialize);

        /// <summary>
        /// Delegates one incremental import to the shared base implementation using IP-based loaders and mapper.
        /// </summary>
        private async Task ProcessIncrementalImportIpBased(ImportControl import) =>
            await ProcessIncrementalImport(import, HandleRuleImportIpBased, HandleOwnerImportIpBased, BuildNewRuleOwnersIpBased);

        public List<RuleOwner> BuildNewRuleOwnersIpBased(List<Rule> rulesToMap, List<FwoOwner> ownersToMap)
        {
            var newRuleOwners = new List<RuleOwner>();
            var ownerNetworksPrepared = PrepareOwnerNetworks(ownersToMap);

            foreach (var rule in rulesToMap)
            {
                var matchesByOwner = GetMatchingOwnerIds(rule, ownerNetworksPrepared);

                foreach (var matchByOwner in matchesByOwner)
                {
                    var minimalMatchedObjectsForOwner = matchByOwner.Value.ToDictionary(
                        dirKvp => dirKvp.Key,
                        dirKvp => dirKvp.Value.Select(o => new
                        {
                            o.Id,
                            o.Name,
                            o.IP,
                            o.IpEnd,
                            OverlappingRanges = o.OverlappingRanges?.Select(r => new
                            {
                                Begin = r.Begin.ToString(),
                                End = r.End.ToString()
                            }).ToList()
                        }).ToList()
                    );

                    newRuleOwners.Add(new RuleOwner
                    {
                        RuleId = rule.Id,
                        OwnerId = matchByOwner.Key,
                        RuleMetadataId = rule.Metadata.Id,
                        OwnerMappingSourceId = (int)OwnerMappingSourceStm.IpBased,
                        MatchedObjects = JsonSerializer.Serialize(minimalMatchedObjectsForOwner)
                    });
                }
            }

            return newRuleOwners;
        }

        /// <summary>
        /// Delegates loading of changed rules, mapping owners, and removable mappings for an IP-based rule import.
        /// </summary>
        private async Task<(List<Rule> rulesToMap, List<FwoOwner> owners, List<RuleOwner> RuleOwnersToRemove)> HandleRuleImportIpBased(ImportControl import) =>
            await HandleRuleImport(import, RuleQueries.getChangedRulesForRuleOwnerMappingIpBased, () => apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwnersForRuleOwnerIpBased));

        private async Task<(List<Rule> RulesToMap, List<FwoOwner> owners, List<RuleOwner> RuleOwnersToRemove)> HandleOwnerImportIpBased(ImportControl import)
        {
            var changelogOwners = await apiConnection.SendQueryAsync<List<OwnerChange>>(OwnerQueries.getChangedOwnersForRuleOwnerMappingIpBased, new { controlId = import.ControlId });
            var ownersToAdd = new List<FwoOwner>();
            var ownersToRemove = new List<FwoOwner>();
            var ruleOwnersToRemove = new List<RuleOwner>();
            var rulesToMap = new List<Rule>();

            if (!ProcessOwnerChanges(changelogOwners, ownersToAdd, ownersToRemove))
            {
                return (new List<Rule>(), new List<FwoOwner>(), new List<RuleOwner>());
            }

            if (ownersToAdd.Any())
            {
                rulesToMap = await apiConnection.SendQueryAsync<List<Rule>>(RuleQueries.getRulesForRuleOwnerIpBased);
            }

            if (ownersToRemove.Any())
            {
                ruleOwnersToRemove = await apiConnection.SendQueryAsync<List<RuleOwner>>(OwnerQueries.getRuleOwnerToRemoveByOwner, new { ownerIds = ownersToRemove.Select(o => o.Id).ToList() });
            }
            return (rulesToMap, ownersToAdd, ruleOwnersToRemove);
        }

        public static (IPAddressRange? range, AddressFamily? ipVersion) GetIpRangeAndVersion(string ipStart, string ipEnd)
        {
            var start = ipStart.StripOffUnnecessaryNetmask();
            var end = ipEnd.StripOffUnnecessaryNetmask();

            if (!IPAddress.TryParse(start, out var startIp))
            {
                Log.WriteError(LogMessageTitle, $"Invalid start IP: {start}");
                return (null, null);
            }

            if (!IPAddress.TryParse(end, out var endIp))
            {
                Log.WriteError(LogMessageTitle, $"Invalid end IP: {end}");
                return (null, null);
            }

            if (startIp.AddressFamily != endIp.AddressFamily)
            {
                Log.WriteError(LogMessageTitle, $"IP families do not match: {start}-{end}");
                return (null, null);
            }

            // compare start and end IPs to ensure start is less than or equal to end
            int cmp = 0;
            if (startIp.AddressFamily == AddressFamily.InterNetwork)
            {
                cmp = IpOperations.IpToUint(startIp).CompareTo(IpOperations.IpToUint(endIp));
            }
            else if (startIp.AddressFamily == AddressFamily.InterNetworkV6)
            {
                cmp = IpOperations.ToBigInteger(startIp).CompareTo(IpOperations.ToBigInteger(endIp));
            }
            else
            {
                Log.WriteError(LogMessageTitle, "Unsupported AddressFamily");
                return (null, null);
            }

            if (cmp > 0)
            {
                Log.WriteError(LogMessageTitle, $"Invalid range: {start}-{end} (start > end)");
                return (null, null);
            }

            var range = IpOperations.GetIPAdressRange($"{start}-{end}");
            return (range, range.Begin.AddressFamily);
        }

        public List<OwnerNetworkPrepared> PrepareOwnerNetworks(List<FwoOwner> ownersToMap)
        {
            return ownersToMap
                    .Where(o => o.OwnerNetworks != null && o.OwnerNetworks.Any())
                    .Select(o => new OwnerNetworkPrepared
                    {
                        OwnerId = o.Id,
                        Ranges = o.OwnerNetworks
                            .Where(nw => !string.IsNullOrWhiteSpace(nw.IP) && !string.IsNullOrWhiteSpace(nw.IpEnd))
                            .Select(nw =>
                            {
                                if (!nw.IP.TryParseIPStringToRange(out var _))
                                {
                                    Log.WriteWarning(LogMessageTitle, $"Invalid owner network format for owner {o.Id}: {nw.IP}-{nw.IpEnd}");
                                }

                                var (range, version) = GetIpRangeAndVersion(nw.IP, nw.IpEnd);

                                if (range == null || version == null)
                                {
                                    Log.WriteWarning(LogMessageTitle, $"Skipping owner network with invalid IP range for owner {o.Id}: {nw.IP}-{nw.IpEnd}");
                                    return null;
                                }

                                return new OwnerRange
                                {
                                    Range = range,
                                    IpVersion = version
                                };
                            })
                            .Where(x => x != null)
                            .ToList()
                    })
                    .ToList();
        }

        public static Dictionary<int, Dictionary<string, List<NetworkObject>>> GetMatchingOwnerIds(Rule rule, List<OwnerNetworkPrepared> ownerNetworksPrepared)
        {
            var matchesByOwner = new Dictionary<int, Dictionary<string, List<NetworkObject>>>();

            var ruleNetworksWithDirections = rule.Froms.Where(n => n?.Object != null).Select(n => (Obj: n.Object!, Direction: "From"))
                                                       .Concat
                                             (rule.Tos.Where(n => n?.Object != null).Select(n => (Obj: n.Object!, Direction: "To"))).ToList();

            if (!ruleNetworksWithDirections.Any())
            {
                Log.WriteWarning(LogMessageTitle, $"Rule {rule.Id} has no network locations and will be skipped.");
                return matchesByOwner;
            }

            // Iterate through each network location of the rule
            foreach (var (obj, direction) in ruleNetworksWithDirections)
            {
                if (obj == null || string.IsNullOrWhiteSpace(obj.IP) || string.IsNullOrWhiteSpace(obj.IpEnd))
                {
                    continue;
                }

                var (ruleRange, ruleIpVersion) = GetIpRangeAndVersion(obj.IP, obj.IpEnd);

                if (ruleRange == null || ruleIpVersion == null)
                {
                    continue;
                }

                foreach (var owner in ownerNetworksPrepared)
                {
                    var overlappingRanges = owner.Ranges
                        .Where(r => r != null && r.IpVersion == ruleIpVersion)
                        .Select(r => IpOperations.GetIntersection(ruleRange, r!.Range))
                        .Where(intersection => intersection != null)
                        .ToList();

                    if (!overlappingRanges.Any())
                    {
                        continue;
                    }

                    if (!matchesByOwner.TryGetValue(owner.OwnerId, out var dictForOwner))
                    {
                        dictForOwner = new Dictionary<string, List<NetworkObject>>();
                        matchesByOwner[owner.OwnerId] = dictForOwner;
                    }

                    if (!dictForOwner.ContainsKey(direction))
                    {
                        dictForOwner[direction] = new List<NetworkObject>();
                    }

                    dictForOwner[direction].Add(new NetworkObject
                    {
                        Id = obj.Id,
                        Name = obj.Name,
                        IP = obj.IP,
                        IpEnd = obj.IpEnd,
                        OverlappingRanges = overlappingRanges
                            .Where(r => r != null)
                            .Select(r => r!)
                            .ToList()
                    });
                }
            }
            return matchesByOwner;
        }

        public class OwnerNetworkPrepared
        {
            public int OwnerId { get; set; }
            public List<OwnerRange?> Ranges { get; set; } = new List<OwnerRange?>();
        }

        public class OwnerRange
        {
            public IPAddressRange Range { get; set; } = default!;
            public AddressFamily? IpVersion { get; set; }
        }
    }
}
