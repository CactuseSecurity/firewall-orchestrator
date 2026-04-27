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

        private async Task<bool> RunFullReinitialize()
        {
            var rulesTask = apiConnection.SendQueryAsync<List<Rule>>(RuleQueries.getRulesForOwnerMappingIpBased);
            var ownersTask = apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwnersForRuleOwnerIpBased);
            await Task.WhenAll(rulesTask, ownersTask);
            var rules = rulesTask.Result;
            var owners = ownersTask.Result;

            var newRuleOwners = BuildNewRuleOwnersIpBased(rules, owners);

            if (!newRuleOwners.Any())
            {
                Log.WriteInfo(LogMessageTitle, "No new rule owners to insert. Aborting import.");
                return false;
            }

            long importControlId = await CreateImportControl();

            foreach (RuleOwner ruleOwner in newRuleOwners)
            {
                ruleOwner.Created = importControlId;
            }

            await SetAllActiveRuleOwnersRemoved(importControlId);
            await InsertNewRuleOwners(newRuleOwners);
            await CompleteImportControlFullReInit(importControlId);

            Log.WriteInfo(LogMessageTitle, "FULL rule_owner reinitialize completed.");
            return true;
        }

        private async Task<bool> RunIncremental()
        {
            var pendingImports = await apiConnection.SendQueryAsync<List<ImportControl>>(ImportQueries.getPendingRuleOwnerImports);

            if (pendingImports == null || !pendingImports.Any())
            {
                return false;
            }

            foreach (var import in pendingImports.OrderBy(i => i.ControlId))
            {
                try
                {
                    await ProcessIncrementalImportIpBased(import);
                }
                catch (Exception ex)
                {
                    Log.WriteError(LogMessageTitle, $"Error while processing import_control {import.ControlId}. ", ex);
                    break;
                }
            }

            return true;
        }

        private async Task ProcessIncrementalImportIpBased(ImportControl import)
        {
            List<Rule> rulesToMap = new List<Rule>();
            List<FwoOwner> owners = new List<FwoOwner>();
            List<RuleOwner> ruleOwnersToRemove = new List<RuleOwner>();

            switch (import.ImportTypeId)
            {
                case ImportType.RULE:
                    {
                        (rulesToMap, owners, ruleOwnersToRemove) = await HandleRuleImportIpBased(import);
                        break;
                    }
                case ImportType.OWNER:
                    {
                        (rulesToMap, owners, ruleOwnersToRemove) = await HandleOwnerImportIpBased(import);
                        break;
                    }

                default:
                    {
                        throw new NotSupportedException($"ImportType '{import.ImportTypeId}' is not supported in LoadRulesAndOwnersAsync.");
                    }
            }

            var newRuleOwners = BuildNewRuleOwnersIpBased(rulesToMap, owners);

            foreach (RuleOwner ruleOwner in newRuleOwners)
            {
                ruleOwner.Created = import.ControlId;
            }

            await SetAffectedRuleOwnersRemoved(ruleOwnersToRemove, import.ControlId);

            await InsertNewRuleOwners(newRuleOwners);

            await CompleteImportControl(import.ControlId);
        }

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

        private async Task<(List<Rule> rulesToMap, List<FwoOwner> owners, List<RuleOwner> RuleOwnersToRemove)> HandleRuleImportIpBased(ImportControl import)
        {
            var changelogRules = await apiConnection.SendQueryAsync<List<RuleChange>>(RuleQueries.getChangedRulesForRuleOwnerMappingIpBased, new { controlId = import.ControlId });
            var rulesToMap = new List<Rule>();
            var rulesToRemove = new List<Rule>();
            var owners = new List<FwoOwner>();
            var ruleOwnersToRemove = new List<RuleOwner>();

            if (!ProcessRuleChanges(changelogRules, rulesToMap, rulesToRemove))
            {
                Log.WriteInfo(LogMessageTitle, "No changed rules found. Aborting incremental import.");
                return (new List<Rule>(), new List<FwoOwner>(), new List<RuleOwner>());
            }

            if (rulesToMap.Any())
            {
                owners = await apiConnection.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwnersForRuleOwnerIpBased);
            }

            if (rulesToRemove.Any())
            {
                ruleOwnersToRemove = await apiConnection.SendQueryAsync<List<RuleOwner>>(OwnerQueries.getRuleOwnerToRemoveByRule, new { ruleIds = rulesToRemove.Select(r => r.Id).ToList() });
            }

            return (rulesToMap, owners, ruleOwnersToRemove);
        }

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
                                    Console.WriteLine($"Ungueltige IP in Regel {o.Id}: {nw.IP} - {nw.IpEnd}");
                                }

                                var (range, version) = GetIpRangeAndVersion(nw.IP, nw.IpEnd);

                                if (range == null || version == null)
                                {
                                    Log.WriteWarning(LogMessageTitle, $"Skipping invalid owner network: {nw.IP}-{nw.IpEnd} for Owner {o.Id}");
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
