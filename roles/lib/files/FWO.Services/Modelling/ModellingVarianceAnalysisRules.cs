using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api.Data;
using FWO.Data;
using FWO.Data.Modelling;
using System.Text.Json;

namespace FWO.Services.Modelling
{
    /// <summary>
    /// Part of Variance Analysis Class analysing the rules
    /// </summary>
    public partial class ModellingVarianceAnalysis
    {
        private List<long> AllowedSrcSpecUserAreas { get; set; } = [];
        private List<long> AllowedDestSpecUserAreas { get; set; } = [];
        private List<long> AllowedSrcUpdatableObjAreas { get; set; } = [];
        private List<long> AllowedDestUpdatableObjAreas { get; set; } = [];
        private NetworkObjectComparer networkObjectComparer = new(new());
        private NetworkObjectGroupFlatComparer networkObjectGroupComparer = new(new());
        private NetworkServiceComparer networkServiceComparer = new(new());
        private NetworkServiceGroupComparer networkServiceGroupComparer = new(new());

        /// <summary>
        /// Compares all production rules carrying the marker of the connection with it. There is only one
        /// comparison: the report, the connection status and the request all get the same verdict.
        /// </summary>
        private void AnalyseRules(ModellingConnection conn)
        {
            GetAllowedSpecUserAreas();
            GetAllowedUpdatableObjAreas();
            networkObjectComparer = new(ruleRecognitionOption);
            networkObjectGroupComparer = new(ruleRecognitionOption);
            networkServiceComparer = new(ruleRecognitionOption);
            networkServiceGroupComparer = new(ruleRecognitionOption);
            bool ruleFound = false;
            foreach (var mgt in RelevantManagements)
            {
                List<Rule> connRules = [.. allModelledRules[mgt.Id].Where(r => r.ConnId == conn.Id)];
                if (connRules.Count == 0)
                {
                    continue;
                }
                ruleFound = true;
                if (WritesLabels(mgt))
                {
                    CompareRuleSetToConn(connRules, conn);
                }
                else
                {
                    connRules.ForEach(rule => CompareRuleToConn(rule, conn));
                }
            }
            if (ruleFound)
            {
                CheckForNAT(conn);
            }
            else
            {
                varianceResult.ConnsNotImplemented.Add(conn);
            }
        }

        private void CheckForNAT(ModellingConnection conn)
        {
            if (conn.IsNat() && userConfig.ModRolloutNatHeuristic && varianceResult.OkRules.FirstOrDefault(x => x.ModelledConnection.Id == conn.Id) != null)
            {
                int connSrcObjects = conn.SourceAreas.Count + conn.SourceAppRoles.Count + conn.SourceAppServers.Count + conn.SourceOtherGroups.Count;
                int connDstObjects = conn.DestinationAreas.Count + conn.DestinationAppRoles.Count + conn.DestinationAppServers.Count + conn.DestinationOtherGroups.Count;
                int connSvcObjects = conn.ServiceGroups.Count + conn.Services.Count;
                foreach (var diff in varianceResult.RuleDifferences.Where(x => x.ModelledConnection.Id == conn.Id).Select(d => d.ImplementedRules))
                {
                    foreach (var rule in diff)
                    {
                        if (rule.Froms.Length == connSrcObjects && rule.Tos.Length == connDstObjects && rule.Services.Length == connSvcObjects)
                        {
                            rule.ModellOk = true;
                        }
                    }
                    diff.RemoveAll(x => x.ModellOk);
                }
                varianceResult.RuleDifferences.RemoveAll(x => x.ModelledConnection.Id == conn.Id && x.ImplementedRules.Count == 0);
            }
        }

        private async Task GetRulesForDeletedConns(List<ModellingConnection> dokuOnlyConnections)
        {
            await GetDeletedConnections();
            List<ModellingConnection> RegardedAsDeletedConns = [.. DeletedConns, .. dokuOnlyConnections];
            List<int> RegardedDeletedConnectionIds = RegardedAsDeletedConns.ConvertAll(c => c.Id);
            foreach (var mgt in allModelledRules.Keys)
            {
                List<Rule> rulesForDeletedModels = [];
                foreach (var rule in allModelledRules[mgt].Where(r => !r.ModellFound))
                {
                    if (int.TryParse(FindModelledMarker(rule), out int connId) && RegardedDeletedConnectionIds.Contains(connId))
                    {
                        rulesForDeletedModels.Add(rule);
                    }
                }
                if (rulesForDeletedModels.Count > 0) // NOSONAR: false positive, list is built in loop above
                {
                    varianceResult.DeletedModelsRules.Add(mgt, rulesForDeletedModels);
                }
            }
        }

        private void GetAllowedSpecUserAreas()
        {
            AllowedSrcSpecUserAreas = [];
            AllowedDestSpecUserAreas = [];
            if (userConfig.ModSpecUserAreas != "" && userConfig.ModSpecUserAreas != "[]")
            {
                List<CommonAreaConfig> configItems = JsonSerializer.Deserialize<List<CommonAreaConfig>>(userConfig.ModSpecUserAreas) ?? [];
                foreach (var configItem in configItems.Where(c => AllAreas.FirstOrDefault(a => a.Id == c.AreaId) != null))
                {
                    if (configItem.UseInSrc)
                    {
                        AllowedSrcSpecUserAreas.Add(configItem.AreaId);
                    }
                    if (configItem.UseInDst)
                    {
                        AllowedDestSpecUserAreas.Add(configItem.AreaId);
                    }
                }
            }
        }

        private void GetAllowedUpdatableObjAreas()
        {
            AllowedSrcUpdatableObjAreas = [];
            AllowedDestUpdatableObjAreas = [];
            if (userConfig.ModUpdatableObjAreas != "" && userConfig.ModUpdatableObjAreas != "[]")
            {
                List<CommonAreaConfig> configItems = JsonSerializer.Deserialize<List<CommonAreaConfig>>(userConfig.ModUpdatableObjAreas) ?? [];
                foreach (var configItem in configItems.Where(c => AllAreas.FirstOrDefault(a => a.Id == c.AreaId) != null))
                {
                    if (configItem.UseInSrc)
                    {
                        AllowedSrcUpdatableObjAreas.Add(configItem.AreaId);
                    }
                    if (configItem.UseInDst)
                    {
                        AllowedDestUpdatableObjAreas.Add(configItem.AreaId);
                    }
                }
            }
        }

        /// <summary>Compares a production rule carrying the marker of the connection with it.</summary>
        private void CompareRuleToConn(Rule rule, ModellingConnection conn)
        {
            bool isImpl = IsNwPartImplementation(rule, conn);
            isImpl &= IsSvcPartImplementation([rule], conn, ruleRecognitionOption.SvcResolveGroup);
            RecordVerdict([rule], conn, isImpl);
        }

        /// <summary>
        /// Compares all rules of a management that writes labels carrying the marker of the connection, together, with it.
        /// Such a management splits a connection with services of different protocols into one rule per protocol, as all
        /// ports of a rule apply to all its protocols. So each rule has to implement the network part of the connection, and
        /// the services of all rules together its services. Service groups are compared resolved, as these managements know
        /// no service groups. All rules get the same verdict.
        /// </summary>
        private void CompareRuleSetToConn(List<Rule> rules, ModellingConnection conn)
        {
            bool isImpl = true;
            foreach (Rule rule in rules)
            {
                isImpl &= IsNwPartImplementation(rule, conn);
            }
            isImpl &= IsSvcPartImplementation(rules, conn, true);
            RecordVerdict(rules, conn, isImpl);
        }

        private void RecordVerdict(List<Rule> rules, ModellingConnection conn, bool isImpl)
        {
            foreach (Rule rule in rules)
            {
                rule.ModellFound = true;
                if (isImpl)
                {
                    conn.ProdRuleFound = true;
                    rule.ModellOk = true;
                    varianceResult.AddOkRule(conn, rule);
                }
                else
                {
                    varianceResult.AddDifference(conn, rule);
                }
            }
        }

        /// <summary>
        /// Whether the rule is an active accept rule whose sources and destinations implement the connection; fills the
        /// disregarded network locations and the unused special user and updatable objects of the rule.
        /// </summary>
        private bool IsNwPartImplementation(Rule rule, ModellingConnection conn)
        {
            bool isImpl = !rule.IsDropRule() && !rule.Disabled && !rule.SourceNegated && !rule.DestinationNegated;
            Dictionary<string, bool> SpecialUserObjects = conn.GetSpecialUserObjectNames();
            Dictionary<string, bool> UpdatableObjects = conn.GetUpdatableObjectNames();
            List<NetworkLocation> disregardedFroms = [];
            List<NetworkLocation> disregardedTos = [];

            isImpl &= IsNwImplementation(rule.Froms, SpecialUserObjects, UpdatableObjects, conn, true, ref disregardedFroms);
            isImpl &= IsNwImplementation(rule.Tos, SpecialUserObjects, UpdatableObjects, conn, false, ref disregardedTos);
            rule.DisregardedFroms = [.. disregardedFroms];
            rule.DisregardedTos = [.. disregardedTos];
            rule.UnusedSpecialUserObjects = [.. SpecialUserObjects.Keys.Where(x => !SpecialUserObjects[x])];
            rule.UnusedUpdatableObjects = [.. UpdatableObjects.Keys.Where(x => !UpdatableObjects[x])];
            isImpl &= rule.UnusedSpecialUserObjects.Count == 0 && rule.UnusedUpdatableObjects.Count == 0;
            return isImpl;
        }

        /// <summary>
        /// Whether the services of the rules together implement the services of the connection. The modelled services the
        /// rules miss are recorded at the first rule.
        /// </summary>
        /// <param name="rules">Rules carrying the marker of the connection, at least one.</param>
        /// <param name="conn">The connection.</param>
        /// <param name="resolveServiceGroups">Whether service groups are compared by their members.</param>
        private bool IsSvcPartImplementation(List<Rule> rules, ModellingConnection conn, bool resolveServiceGroups)
        {
            List<NetworkService> normConnSvc = SplitPortRanges(ModellingServiceWrapper.Resolve(conn.Services).ToList().ConvertAll(s => ModellingService.ToNetworkService(s)));
            List<NetworkService> normConnSvcGrp = SplitPortRanges(ModellingServiceGroupWrapper.Resolve(conn.ServiceGroups).ToList().ConvertAll(a => a.ToNetworkServiceGroup()));
            List<List<NetworkService>> normRuleSvcs = rules.ConvertAll(rule => SplitPortRanges([.. rule.Services.ToList().ConvertAll(s => s.Content)]));
            List<NetworkService> allNormRuleSvc = [.. normRuleSvcs.SelectMany(services => services)];
            List<NetworkService> disregardedServices = [];

            bool isSvcImpl = IsSvcImplementation(allNormRuleSvc, normConnSvc, normConnSvcGrp, disregardedServices, resolveServiceGroups);
            if (!isSvcImpl && ruleRecognitionOption.SvcSplitPortRanges)
            {
                disregardedServices = RebundlePortRanges(disregardedServices);
                for (int index = 0; index < rules.Count; index++)
                {
                    rules[index].Services = [.. RebundlePortRanges(normRuleSvcs[index]).ConvertAll(s => new ServiceWrapper() { Content = s })];
                }
            }
            for (int index = 0; index < rules.Count; index++)
            {
                rules[index].DisregardedServices = index == 0 ? [.. disregardedServices] : [];
            }
            return isSvcImpl;
        }

        private List<NetworkService> SplitPortRanges(List<NetworkService> servicesIn)
        {
            if (!ruleRecognitionOption.SvcSplitPortRanges)
            {
                return servicesIn;
            }
            List<NetworkService> servicesOut = [];
            foreach (var svc in servicesIn)
            {
                if (svc.Type.Name == ObjectType.Group)
                {
                    List<NetworkService> grpMembers = [];
                    foreach (var member in svc.ServiceGroupFlats.Select(m => m.Object).Where(m => m != null))
                    {
                        grpMembers.AddRange(SplitPortRange(member!));
                    }
                    svc.ServiceGroupFlats = Array.ConvertAll(grpMembers.ToArray(), o => new GroupFlat<NetworkService>() { Object = o });
                    servicesOut.Add(svc);
                }
                else
                {
                    servicesOut.AddRange(SplitPortRange(svc));
                }
            }
            return servicesOut;
        }

        private static List<NetworkService> SplitPortRange(NetworkService serviceIn)
        {
            List<NetworkService> servicesOut = [];
            if (serviceIn.DestinationPort == null || serviceIn.DestinationPortEnd == null || serviceIn.DestinationPortEnd <= serviceIn.DestinationPort)
            {
                return [serviceIn];
            }
            else
            {
                for (int port = (int)serviceIn.DestinationPort; port <= (int)serviceIn.DestinationPortEnd; port++)
                {
                    NetworkService partialSvc = new(serviceIn) { DestinationPort = port, DestinationPortEnd = port };
                    servicesOut.Add(partialSvc);
                }
            }
            return servicesOut;
        }

        private static List<NetworkService> RebundlePortRanges(List<NetworkService> servicesIn)
        {
            List<NetworkService> servicesOut = [];
            NetworkService? actSvc = null;
            foreach (var svc in servicesIn)
            {
                if (svc.DestinationPort == null)
                {
                    servicesOut.Add(svc);
                }
                else if (actSvc == null)
                {
                    actSvc = svc;
                }
                else if (svc.Id == actSvc.Id && svc.DestinationPort == actSvc.DestinationPortEnd + 1 && svc.IsSurplus == actSvc.IsSurplus)
                {
                    actSvc.DestinationPortEnd++;
                }
                else
                {
                    servicesOut.Add(actSvc);
                    actSvc = svc;
                }
            }
            if (actSvc != null)
            {
                servicesOut.Add(actSvc);
            }
            return servicesOut;
        }

        private bool IsNwImplementation(NetworkLocation[] networkLocations, Dictionary<string, bool> specialUserObjects,
            Dictionary<string, bool> updatableObjects, ModellingConnection conn, bool source, ref List<NetworkLocation> disregardedLocations)
        {
            (List<ModellingAppServerWrapper> appServers, List<ModellingAppRoleWrapper> appRoles,
                List<ModellingNetworkAreaWrapper> areas, List<ModellingNwGroupWrapper> otherGroups) = GetNwImplementationConfiguration(conn, source);
            int specialUserAreaCount = GetRelevantAreaCount(areas, source ? AllowedSrcSpecUserAreas : AllowedDestSpecUserAreas);
            int updatableObjectAreaCount = GetRelevantAreaCount(areas, source ? AllowedSrcUpdatableObjAreas : AllowedDestUpdatableObjAreas);

            ResetSurplusFlags(networkLocations);
            CompareNetworkLocations(networkLocations, appServers, appRoles, areas, otherGroups, disregardedLocations);
            AdjustWithSpecialUserObjects(networkLocations, specialUserObjects, source, specialUserAreaCount, ref disregardedLocations);
            AdjustWithUpdatableObjects(networkLocations, updatableObjects, source, updatableObjectAreaCount, ref disregardedLocations);
            return disregardedLocations.Count == 0 && networkLocations.Where(n => n.Object.IsSurplus).ToList().Count == 0;
        }

        /// <summary>
        /// Gets the modelled network objects for the requested connection direction.
        /// </summary>
        private static (List<ModellingAppServerWrapper> AppServers, List<ModellingAppRoleWrapper> AppRoles,
            List<ModellingNetworkAreaWrapper> Areas, List<ModellingNwGroupWrapper> OtherGroups) GetNwImplementationConfiguration(ModellingConnection conn, bool source)
        {
            return source
                ? (conn.SourceAppServers, conn.SourceAppRoles, conn.SourceAreas, conn.SourceOtherGroups)
                : (conn.DestinationAppServers, conn.DestinationAppRoles, conn.DestinationAreas, conn.DestinationOtherGroups);
        }

        /// <summary>
        /// Counts the distinct modelled areas that are configured for special handling.
        /// </summary>
        private static int GetRelevantAreaCount(List<ModellingNetworkAreaWrapper> areas, List<long> allowedAreaIds)
        {
            return ModellingNetworkAreaWrapper.Resolve(areas)
                .Select(area => (long)area.Id)
                .Distinct()
                .Count(allowedAreaIds.Contains);
        }

        /// <summary>
        /// Clears surplus markers left by a preceding network-object comparison.
        /// </summary>
        private static void ResetSurplusFlags(NetworkLocation[] networkLocations)
        {
            foreach (var location in networkLocations)
            {
                location.Object.IsSurplus = false;
            }
        }

        /// <summary>
        /// Compares network areas, application servers, and unresolved network groups. Missing modelled objects
        /// are collected in disregardedLocations, surplus production objects are marked with IsSurplus.
        /// </summary>
        private void CompareNetworkLocations(NetworkLocation[] networkLocations, List<ModellingAppServerWrapper> appServers,
            List<ModellingAppRoleWrapper> appRoles, List<ModellingNetworkAreaWrapper> areas, List<ModellingNwGroupWrapper> otherGroups,
            List<NetworkLocation> disregardedLocations)
        {
            CompareNwAreas(networkLocations, areas, disregardedLocations);
            CompareAppServers(networkLocations, appServers, appRoles, disregardedLocations);
            if (!ruleRecognitionOption.NwResolveGroup)
            {
                CompareRemainingNwGroups(networkLocations, appRoles, otherGroups, disregardedLocations);
            }
        }

        private void AdjustWithSpecialUserObjects(NetworkLocation[] networkLocations, Dictionary<string, bool> specialUserObjects, bool source,
            int specialUserAreaCount, ref List<NetworkLocation> disregardedLocations)
        {
            if (specialUserObjects.Count > 0 && disregardedLocations.Count > 0)
            {
                List<NetworkLocation> specUserLocations = GetPlaceholderSubstitutes(networkLocations, specialUserObjects);
                List<NetworkLocation> remainingPossibleSpecObj = GetPossibleSpecObjects(disregardedLocations, source);
                // A placeholder area stands in for any number of special user objects (so N objects cover 1 area),
                // but a single object must not cover several areas - require at least as many objects as areas.
                if (remainingPossibleSpecObj.Count > 0 && specUserLocations.Count >= specialUserAreaCount)
                {
                    foreach (var location in remainingPossibleSpecObj)
                    {
                        disregardedLocations.Remove(location);
                    }
                    foreach (var specUser in specUserLocations.Select(s => s.Object))
                    {
                        specUser.IsSurplus = false;
                        specialUserObjects[specUser.Name.ToLower()] = true;
                    }
                }
            }
        }

        private List<NetworkLocation> GetPossibleSpecObjects(List<NetworkLocation> disregardedLocations, bool source)
        {
            return [.. disregardedLocations.Where(l => l.Object.Type.Name == ObjectType.Group && (source ? AllowedSrcSpecUserAreas.Contains(l.Object.Id) : AllowedDestSpecUserAreas.Contains(l.Object.Id)))];
        }

        /// <summary>
        /// Selects production objects, matched by name, that may stand in for a modelled placeholder area.
        /// IP-less imported objects must be counted explicitly while IP comparison is active: the comparer
        /// can deduplicate several IP-less objects before all of them receive IsSurplus.
        /// </summary>
        private List<NetworkLocation> GetPlaceholderSubstitutes(NetworkLocation[] networkLocations, Dictionary<string, bool> objectsByName)
        {
            return [.. networkLocations.Where(n => objectsByName.ContainsKey(n.Object.Name.ToLower())
                    && (n.Object.IsSurplus || IsIpLessObjectWhileIpComparisonIsActive(n.Object)))];
        }

        private bool IsIpLessObjectWhileIpComparisonIsActive(NetworkObject networkObject)
        {
            return ruleRecognitionOption.NwRegardIp && string.IsNullOrEmpty(networkObject.IP);
        }

        private void AdjustWithUpdatableObjects(NetworkLocation[] networkLocations, Dictionary<string, bool> updatableObjects, bool source,
            int updatableObjectAreaCount, ref List<NetworkLocation> disregardedLocations)
        {
            if (updatableObjects.Count > 0 && disregardedLocations.Count > 0)
            {
                List<NetworkLocation> updObjLocations = GetPlaceholderSubstitutes(networkLocations, updatableObjects);
                List<NetworkLocation> remainingPossibleUpdatableObj = GetPossibleUpdatableObjects(disregardedLocations, source);
                // A placeholder area stands in for any number of updatable objects (so N objects cover 1 area),
                // but a single object must not cover several areas - require at least as many objects as areas.
                if (remainingPossibleUpdatableObj.Count > 0 && updObjLocations.Count >= updatableObjectAreaCount)
                {
                    foreach (var location in remainingPossibleUpdatableObj)
                    {
                        disregardedLocations.Remove(location);
                    }
                    foreach (var updObj in updObjLocations.Select(s => s.Object))
                    {
                        updObj.IsSurplus = false;
                        updatableObjects[updObj.Name.ToLower()] = true;
                    }
                }
            }
        }

        private List<NetworkLocation> GetPossibleUpdatableObjects(List<NetworkLocation> disregardedLocations, bool source)
        {
            return [.. disregardedLocations.Where(l => l.Object.Type.Name == ObjectType.Group && (source ? AllowedSrcUpdatableObjAreas.Contains(l.Object.Id) : AllowedDestUpdatableObjAreas.Contains(l.Object.Id)))];
        }

        private void CompareNwAreas(NetworkLocation[] networkLocations, List<ModellingNetworkAreaWrapper> areas, List<NetworkLocation> disregardedLocations)
        {
            List<NetworkObject> allProdNwAreas = networkLocations.Where(n => n.Object.Type.Name == ObjectType.Group && IsArea(n.Object)).ToList().ConvertAll(n => n.Object);
            List<NetworkObject> allModNwAreas = ModellingNetworkAreaWrapper.Resolve(areas).ToList().ConvertAll(a => a.ToNetworkObjectGroup(true));
            NetworkObjectComparer networkAreaComparer = new(new() { NwRegardName = true, NwRegardIp = false });
            CompareNwObjects(allModNwAreas, allProdNwAreas, networkLocations, disregardedLocations, networkAreaComparer);
        }

        private bool IsArea(NetworkObject nwGroup)
        {
            return nwGroup.Name.StartsWith(namingConvention.NetworkAreaPattern);
        }

        private void CompareAppServers(NetworkLocation[] networkLocations, List<ModellingAppServerWrapper> appServers,
            List<ModellingAppRoleWrapper> appRoles, List<NetworkLocation> disregardedLocations)
        {
            List<NetworkObject> allProdNwObjects = networkLocations.Where(n => n.Object.Type.Name != ObjectType.Group).ToList().ConvertAll(n => n.Object);
            List<NetworkObject> allModNwObjects = ModellingAppServerWrapper.Resolve(appServers).ToList().ConvertAll(a => ModellingAppServer.ToNetworkObject(a));
            if (ruleRecognitionOption.NwResolveGroup)
            {
                foreach (var nwGroup in networkLocations.Where(n => n.Object.Type.Name == ObjectType.Group && !IsArea(n.Object)))
                {
                    allProdNwObjects.AddRange(GetResolvedGroupMembers(nwGroup.Object));
                }
                foreach (var appRole in ModellingAppRoleWrapper.Resolve(appRoles))
                {
                    allModNwObjects.AddRange(ModellingAppServerWrapper.Resolve(appRole.AppServers).ToList().ConvertAll(a => ModellingAppServer.ToNetworkObject(a)));
                }
            }
            CompareNwObjects(allModNwObjects, allProdNwObjects, networkLocations, disregardedLocations, networkObjectComparer);
        }

        /// <summary>
        /// Gets the members of a resolved production group. The flat entries of a group also hold the group
        /// itself and its nested groups, which are no members to compare with the modelled app servers.
        /// </summary>
        private static List<NetworkObject> GetResolvedGroupMembers(NetworkObject nwGroup)
        {
            return [.. nwGroup.ObjectGroupFlats
                .Where(flat => flat.Object != null && flat.Object.Type.Name != ObjectType.Group)
                .Select(flat => flat.Object!)];
        }

        private void CompareRemainingNwGroups(NetworkLocation[] networkLocations, List<ModellingAppRoleWrapper> appRoles, List<ModellingNwGroupWrapper> otherGroups, List<NetworkLocation> disregardedLocations)
        {
            List<NetworkObject> allProdNwGroups = networkLocations.Where(n => n.Object.Type.Name == ObjectType.Group && !IsArea(n.Object)).ToList().ConvertAll(n => n.Object);
            List<NetworkObject> allModNwGroups = ModellingAppRoleWrapper.Resolve(appRoles).ToList().ConvertAll(a => a.ToNetworkObjectGroup(true));
            allModNwGroups.AddRange(ModellingNwGroupWrapper.Resolve(otherGroups).ToList().ConvertAll(a => a.ToNetworkObjectGroup()));
            CompareNwObjects(allModNwGroups, allProdNwGroups, networkLocations, disregardedLocations, networkObjectGroupComparer);
        }

        /// <summary>
        /// Compares modelled and production objects as sets: object multiplicity never counts as a variance,
        /// so the deduplicating Except decides equality. Missing modelled objects are added to
        /// disregardedLocations, surplus production objects are marked with IsSurplus.
        /// </summary>
        private static void CompareNwObjects(List<NetworkObject> allModGroups, List<NetworkObject> allProdGroups, NetworkLocation[] networkLocations,
            List<NetworkLocation> disregardedLocations, IEqualityComparer<NetworkObject?> comparer)
        {
            List<NetworkObject> disregardedGroups = [.. allModGroups.Except(allProdGroups, comparer)];
            List<NetworkObject> surplusGroups = [.. allProdGroups.Except(allModGroups, comparer)];
            foreach (var obj in surplusGroups)
            {
                MarkSurplus(networkLocations, obj);
            }
            disregardedLocations.AddRange(disregardedGroups.ConvertAll(o => new NetworkLocation(new(), o)));
        }

        /// <summary>
        /// Marks the rule location holding a surplus object. If the object is a member of a resolved group
        /// (resolve groups option), the group itself is marked, so the surplus member is not lost.
        /// </summary>
        private static void MarkSurplus(NetworkLocation[] networkLocations, NetworkObject surplusObject)
        {
            NetworkLocation? directLocation = networkLocations.FirstOrDefault(n => n.Object.Id == surplusObject.Id);
            if (directLocation != null)
            {
                directLocation.Object.IsSurplus = true;
                return;
            }
            foreach (NetworkLocation groupLocation in networkLocations.Where(n => n.Object.ObjectGroupFlats.Any(f => f.Object?.Id == surplusObject.Id)))
            {
                groupLocation.Object.IsSurplus = true;
            }
        }

        private bool IsSvcImplementation(List<NetworkService> prodServices, List<NetworkService> modServices, List<NetworkService> modServiceGroups,
            List<NetworkService> disregardedServices, bool resolveServiceGroups)
        {
            bool isImpl = CompareServices(prodServices, modServices, modServiceGroups, disregardedServices, resolveServiceGroups);
            return (resolveServiceGroups || CompareSvcGroups(prodServices, modServiceGroups, disregardedServices)) && isImpl;
        }

        private bool CompareServices(List<NetworkService> prodServices, List<NetworkService> modServices, List<NetworkService> modServiceGroups,
            List<NetworkService> disregardedServices, bool resolveServiceGroups)
        {
            List<NetworkService> allProdServices = [.. prodServices.Where(s => s.Type.Name != ServiceType.Group)];
            List<NetworkService> allModServices = modServices;
            if (resolveServiceGroups)
            {
                foreach (var svc in prodServices.Where(n => n.Type.Name == ServiceType.Group))
                {
                    allProdServices.AddRange(GetResolvedGroupMembers(svc));
                }
                foreach (var svcGrp in modServiceGroups)
                {
                    allModServices.AddRange([.. svcGrp.ServiceGroupFlats.ToList().ConvertAll(g => g.Object ?? new())]);
                }
            }
            return CompareSvcObjects(allModServices, allProdServices, prodServices, disregardedServices, networkServiceComparer);
        }

        /// <summary>
        /// Gets the members of a resolved production service group. The flat entries of a group also hold the
        /// group itself and its nested groups, which are no members to compare with the modelled services.
        /// </summary>
        private static List<NetworkService> GetResolvedGroupMembers(NetworkService svcGroup)
        {
            return [.. svcGroup.ServiceGroupFlats
                .Where(flat => flat.Object != null && flat.Object.Type.Name != ServiceType.Group)
                .Select(flat => flat.Object!)];
        }

        private bool CompareSvcGroups(List<NetworkService> prodServices, List<NetworkService> modServiceGroups, List<NetworkService> disregardedServices)
        {
            List<NetworkService> allProdSvcGroups = [.. prodServices.Where(n => n.Type.Name == ServiceType.Group)];
            List<NetworkService> allModSvcGroups = modServiceGroups;
            return CompareSvcObjects(allModSvcGroups, allProdSvcGroups, prodServices, disregardedServices, networkServiceGroupComparer);
        }

        /// <summary>
        /// Compares modelled and production services as sets: multiplicity never counts as a variance.
        /// Missing modelled services are added to disregardedServices, surplus ones are marked with IsSurplus.
        /// </summary>
        private static bool CompareSvcObjects(List<NetworkService> allModSvcObjects, List<NetworkService> allProdSvcObjects, List<NetworkService> prodServices,
            List<NetworkService> disregardedServices, IEqualityComparer<NetworkService?> comparer)
        {
            List<NetworkService> disregardedSvcObjects = [.. allModSvcObjects.Except(allProdSvcObjects, comparer)];
            List<NetworkService> surplusSvcObjects = [.. allProdSvcObjects.Except(allModSvcObjects, comparer)];
            foreach (var svc in surplusSvcObjects)
            {
                NetworkService? exSvc = prodServices.FirstOrDefault(n => n.Id == svc.Id && n.DestinationPort == svc.DestinationPort);
                if (exSvc != null)
                {
                    exSvc.IsSurplus = true;
                }
            }
            disregardedServices.AddRange(disregardedSvcObjects);
            return disregardedSvcObjects.Count == 0 && surplusSvcObjects.Count == 0;
        }
    }
}
