using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;

namespace FWO.Services
{
    /// <summary>
	/// Part of Variance Analysis Class analysing the rules
	/// </summary>
    public partial class ModellingVarianceAnalysis
    {
        private NetworkObjectComparer networkObjectComparer = new(new());
        private NetworkObjectGroupFlatComparer networkObjectGroupComparer = new(new());
        private NetworkServiceComparer networkServiceComparer = new(new());
        private NetworkServiceGroupComparer networkServiceGroupComparer = new(new());
        private bool FullAnalysis = false;

        private void AnalyseRules(ModellingConnection conn, bool fullAnalysis)
        {
            FullAnalysis = fullAnalysis;
            networkObjectComparer = new(ruleRecognitionOption);
            networkObjectGroupComparer = new(ruleRecognitionOption);
            networkServiceComparer = new(ruleRecognitionOption);
            networkServiceGroupComparer = new(ruleRecognitionOption);
            bool ruleFound = false;
            foreach (var mgt in RelevantManagements)
            {
                foreach(var rule in allModelledRules[mgt.Id])
                {
                    if(CompareRuleToConn(rule, conn))
                    {
                        ruleFound = true;
                    }
                }
            }
            if(!ruleFound)
            {
                varianceResult.ConnsNotImplemented.Add(conn);
            }
        }

        private bool CompareRuleToConn(Rule rule, ModellingConnection conn)
        {
            if(rule.ConnId == conn.Id)
            {
                if(IsImplementation(rule, conn))
                {
                    conn.ProdRuleFound = true;
                    rule.ModellOk = true;
                }
                else
                {
                    varianceResult.AddDifference(conn, rule);
                }
                return true;
            }
            return false;
        }

        private bool IsImplementation(Rule rule, ModellingConnection conn)
        {
            bool isImpl = !rule.IsDropRule() && !rule.Disabled && !rule.SourceNegated && !rule.DestinationNegated;
            Dictionary<string, bool> SpecialUserObjects = conn.GetSpecialUserObjectNames();
            List<NetworkLocation> disregardedFroms = [];
            List<NetworkLocation> disregardedTos = [];
            List<NetworkService> disregardedServices = [];
            (List<NetworkService> normConnSvc, List<NetworkService> normConnSvcGrp, List<NetworkService> normRuleSvc) = NormalizeServices(rule, conn);
            if(FullAnalysis)
            {
                isImpl &= IsNwImplementation(rule.Froms, SpecialUserObjects, conn.SourceAppServers, conn.SourceAppRoles, conn.SourceAreas, conn.SourceOtherGroups, ref disregardedFroms);
                isImpl &= IsNwImplementation(rule.Tos, SpecialUserObjects, conn.DestinationAppServers, conn.DestinationAppRoles, conn.DestinationAreas, conn.DestinationOtherGroups, ref disregardedTos);
                bool isSvcImpl = IsSvcImplementation(normRuleSvc, normConnSvc, normConnSvcGrp, disregardedServices);
                isImpl &= isSvcImpl;
                if(!isSvcImpl && ruleRecognitionOption.SvcSplitPortRanges)
                {
                    disregardedServices = RebundlePortRanges(disregardedServices);
                    rule.Services = [.. RebundlePortRanges(normRuleSvc).ConvertAll(s => new ServiceWrapper(){ Content = s })];
                }
                rule.DisregardedFroms = [.. disregardedFroms];
                rule.DisregardedTos = [.. disregardedTos];
                rule.DisregardedServices = [.. disregardedServices];
                rule.UnusedSpecialUserObjects = [.. SpecialUserObjects.Keys.Where(x => !SpecialUserObjects[x])];
                isImpl &= rule.UnusedSpecialUserObjects.Count == 0;
            }
            else if (isImpl)
            {
                isImpl = IsNwImplementation(rule.Froms, SpecialUserObjects, conn.SourceAppServers, conn.SourceAppRoles, conn.SourceAreas, conn.SourceOtherGroups, ref disregardedFroms)
                    && IsNwImplementation(rule.Tos, SpecialUserObjects, conn.DestinationAppServers, conn.DestinationAppRoles, conn.DestinationAreas, conn.DestinationOtherGroups, ref disregardedTos)
                    && IsSvcImplementation(normRuleSvc, normConnSvc, normConnSvcGrp, [])
                    && !SpecialUserObjects.Any(x => !x.Value);
            }
            return isImpl;
        }

        private (List<NetworkService>, List<NetworkService> normConnSvcGrp, List<NetworkService> normRuleSvc) NormalizeServices(Rule rule, ModellingConnection conn)
        {
            List<NetworkService> normConnSvc = SplitPortRanges(ModellingServiceWrapper.Resolve(conn.Services).ToList().ConvertAll(s => ModellingService.ToNetworkService(s)));
            List<NetworkService> normConnSvcGrp = SplitPortRanges(ModellingServiceGroupWrapper.Resolve(conn.ServiceGroups).ToList().ConvertAll(a => a.ToNetworkServiceGroup()));
            List<NetworkService> normRuleSvc = SplitPortRanges([.. rule.Services.ToList().ConvertAll(s => s.Content)]);
            return(normConnSvc, normConnSvcGrp, normRuleSvc);
        }

        private List<NetworkService> SplitPortRanges(List<NetworkService> servicesIn)
        {
            if(!ruleRecognitionOption.SvcSplitPortRanges)
            {
                return servicesIn;
            }
            List<NetworkService> servicesOut = [];
            foreach(var svc in servicesIn)
            {
                if(svc.Type.Name == ObjectType.Group)
                {
                    List<NetworkService> grpMembers = [];
                    foreach(var member in svc.ServiceGroupFlats)
                    {
                        if (member.Object != null)
                        {
                            grpMembers.AddRange(SplitPortRange(member.Object));
                        }
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
            if(serviceIn.DestinationPort == null ||serviceIn.DestinationPortEnd == null || serviceIn.DestinationPortEnd <= serviceIn.DestinationPort)
            {
                return [serviceIn];
            }
            else
            {
                for (int port = (int)serviceIn.DestinationPort; port <= (int)serviceIn.DestinationPortEnd; port++)
                {
                    NetworkService partialSvc = new(serviceIn) { DestinationPort = port, DestinationPortEnd = port};
                    servicesOut.Add(partialSvc);
                }
            }
            return servicesOut;
        }

        private static List<NetworkService> RebundlePortRanges(List<NetworkService> servicesIn)
        {
            List<NetworkService> servicesOut = [];
            NetworkService? actSvc = null;
            foreach(var svc in servicesIn)
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

        private bool IsNwImplementation(NetworkLocation[] networkLocations, Dictionary<string, bool> specialUserObjects, List<ModellingAppServerWrapper> appServers,
            List<ModellingAppRoleWrapper> appRoles, List<ModellingNetworkAreaWrapper> areas, List<ModellingNwGroupWrapper> otherGroups, ref List<NetworkLocation> disregardedLocations)
        {
            bool specialUserObjectsExist = specialUserObjects.Count > 0;
            foreach(var loc in networkLocations)
            {
                loc.Object.IsSurplus = false;
            }
            if (!CompareNwAreas(networkLocations, areas, disregardedLocations, specialUserObjectsExist) && !FullAnalysis && !specialUserObjectsExist)
            {
                return false;
            }
            if (!CompareAppServers(networkLocations, appServers, appRoles, disregardedLocations, specialUserObjectsExist) && !FullAnalysis && !specialUserObjectsExist)
            {
                return false;
            }
            if (!ruleRecognitionOption.NwResolveGroup && !CompareRemainingNwGroups(networkLocations, appRoles, otherGroups, disregardedLocations, specialUserObjectsExist) && !FullAnalysis && !specialUserObjectsExist)
            {
                return false;
            }
            AdjustWithSpecialUserObjects(networkLocations, specialUserObjects, ref disregardedLocations);
            return disregardedLocations.Count == 0 && networkLocations.Where(n => n.Object.IsSurplus).ToList().Count == 0;
        }

        private static void AdjustWithSpecialUserObjects(NetworkLocation[] networkLocations, Dictionary<string, bool> specialUserObjects, ref List<NetworkLocation> disregardedLocations)
        {
            if(specialUserObjects.Count > 0 && disregardedLocations.Count > 0)
            {
                List<NetworkLocation> surplusNwLocations = [.. networkLocations.Where(n => n.Object.IsSurplus)];
                int userCounter = 0;
                foreach(var location in surplusNwLocations.Where(l => specialUserObjects.ContainsKey(l.Object.Name.ToLower())))
                {
                    specialUserObjects[location.Object.Name.ToLower()] = true;
                    userCounter++;
                }
                if(userCounter <= disregardedLocations.Count)
                {
                    disregardedLocations = [];
                    foreach(var location in surplusNwLocations.Where(l => specialUserObjects.ContainsKey(l.Object.Name.ToLower())))
                    {
                        location.Object.IsSurplus = false;
                    }
                }
             }
        }

        private bool CompareNwAreas(NetworkLocation[] networkLocations, List<ModellingNetworkAreaWrapper> areas, List<NetworkLocation> disregardedLocations, bool specialUserObjectsExist)
        {
            List<NetworkObject> allProdNwAreas = networkLocations.Where(n => n.Object.Type.Name == ObjectType.Group && IsArea(n.Object)).ToList().ConvertAll(n => n.Object);
            List<NetworkObject> allModNwAreas = ModellingNetworkAreaWrapper.Resolve(areas).ToList().ConvertAll(a => a.ToNetworkObjectGroup(true));
            NetworkObjectComparer networkAreaComparer = new(new(){ NwRegardName = true, NwRegardIp = false });
            return CompareNwObjects(allModNwAreas, allProdNwAreas, networkLocations, disregardedLocations, networkAreaComparer, specialUserObjectsExist);
        }

        private bool IsArea(NetworkObject nwGroup)
        {
            return nwGroup.Name.StartsWith(namingConvention.NetworkAreaPattern);
        }

        private bool CompareAppServers(NetworkLocation[] networkLocations, List<ModellingAppServerWrapper> appServers,
            List<ModellingAppRoleWrapper> appRoles, List<NetworkLocation> disregardedLocations, bool specialUserObjectsExist)
        {
            List<NetworkObject> allProdNwObjects = networkLocations.Where(n => n.Object.Type.Name != ObjectType.Group).ToList().ConvertAll(n => n.Object);
            List<NetworkObject> allModNwObjects = ModellingAppServerWrapper.Resolve(appServers).ToList().ConvertAll(a => ModellingAppServer.ToNetworkObject(a));
            if(ruleRecognitionOption.NwResolveGroup)
            {
                foreach(var nwGroup in networkLocations.Where(n => n.Object.Type.Name == ObjectType.Group && !IsArea(n.Object)))
                {
                    allProdNwObjects.AddRange([.. nwGroup.Object.ObjectGroupFlats.Where(g => g.Object != null).ToList().ConvertAll(g => g.Object!)]);
                }
                foreach(var appRole in ModellingAppRoleWrapper.Resolve(appRoles))
                {
                    allModNwObjects.AddRange(ModellingAppServerWrapper.Resolve(appRole.AppServers).ToList().ConvertAll(a => ModellingAppServer.ToNetworkObject(a)));
                }
            }
            return CompareNwObjects(allModNwObjects, allProdNwObjects, networkLocations, disregardedLocations, networkObjectComparer, specialUserObjectsExist);
        }

        private bool CompareRemainingNwGroups(NetworkLocation[] networkLocations, List<ModellingAppRoleWrapper> appRoles, List<ModellingNwGroupWrapper> otherGroups, List<NetworkLocation> disregardedLocations, bool specialUserObjectsExist)
        {
            List<NetworkObject> allProdNwGroups = networkLocations.Where(n => n.Object.Type.Name == ObjectType.Group && !IsArea(n.Object)).ToList().ConvertAll(n => n.Object);
            List<NetworkObject> allModNwGroups = ModellingAppRoleWrapper.Resolve(appRoles).ToList().ConvertAll(a => a.ToNetworkObjectGroup());
            allModNwGroups.AddRange(ModellingNwGroupWrapper.Resolve(otherGroups).ToList().ConvertAll(a => a.ToNetworkObjectGroup()));
            return CompareNwObjects(allModNwGroups, allProdNwGroups, networkLocations, disregardedLocations, networkObjectGroupComparer, specialUserObjectsExist);
        }

        private bool CompareNwObjects(List<NetworkObject> allModGroups, List<NetworkObject> allProdGroups, NetworkLocation[] networkLocations,
            List<NetworkLocation> disregardedLocations, IEqualityComparer<NetworkObject?> comparer, bool specialUserObjectsExist = false)
        {
            if(FullAnalysis || specialUserObjectsExist)
            {
                List<NetworkObject> disregardedGroups = [.. allModGroups.Except(allProdGroups, comparer)];
                List<NetworkObject> surplusGroups = [.. allProdGroups.Except(allModGroups, comparer)];
                foreach (var obj in surplusGroups)
                {
                    NetworkLocation? extLoc = networkLocations.FirstOrDefault(n => n.Object.Id == obj.Id);
                    if(extLoc != null)
                    {
                        extLoc.Object.IsSurplus = true;
                    }
                }
                disregardedLocations.AddRange(disregardedGroups.ConvertAll(o => new NetworkLocation(new(), o)));
                return disregardedGroups.Count == 0 && surplusGroups.Count == 0;
            }

            return allModGroups.Count == allProdGroups.Count
                && allModGroups.Except(allProdGroups, comparer).ToList().Count == 0
                && allProdGroups.Except(allModGroups, comparer).ToList().Count == 0;
        }

        private bool IsSvcImplementation(List<NetworkService> prodServices, List<NetworkService> modServices, List<NetworkService> modServiceGroups, List<NetworkService> disregardedServices)
        {
            bool isImpl = true;
            if(!CompareServices(prodServices, modServices, modServiceGroups, disregardedServices))
            {
                if(!FullAnalysis)
                {
                    return false;
                }
                isImpl = false;
            }
            return (ruleRecognitionOption.SvcResolveGroup || CompareSvcGroups(prodServices, modServiceGroups, disregardedServices)) && isImpl;
        }

        private bool CompareServices(List<NetworkService> prodServices, List<NetworkService> modServices, List<NetworkService> modServiceGroups, List<NetworkService> disregardedServices)
        {
            List<NetworkService> allProdServices = [.. prodServices.Where(s => s.Type.Name != ServiceType.Group)];
            List<NetworkService> allModServices = modServices;
            if(ruleRecognitionOption.SvcResolveGroup)
            {
                foreach(var svc in prodServices.Where(n => n.Type.Name == ServiceType.Group))
                {
                    allProdServices.AddRange([.. svc.ServiceGroupFlats.ToList().ConvertAll(g => g.Object ?? new())]);
                }
                foreach(var svcGrp in modServiceGroups)
                {
                    allModServices.AddRange([.. svcGrp.ServiceGroupFlats.ToList().ConvertAll(g => g.Object ?? new())]);
                }
            }
            return CompareSvcObjects(allModServices, allProdServices, prodServices, disregardedServices, networkServiceComparer);
        }

        private bool CompareSvcGroups(List<NetworkService> prodServices, List<NetworkService> modServiceGroups, List<NetworkService> disregardedServices)
        {
            List<NetworkService> allProdSvcGroups = [.. prodServices.Where(n => n.Type.Name == ServiceType.Group)];
            List<NetworkService> allModSvcGroups = modServiceGroups;
            return CompareSvcObjects(allModSvcGroups, allProdSvcGroups, prodServices, disregardedServices, networkServiceGroupComparer);
        }

        private bool CompareSvcObjects(List<NetworkService> allModSvcObjects, List<NetworkService> allProdSvcObjects, List<NetworkService> prodServices,
            List<NetworkService> disregardedServices, IEqualityComparer<NetworkService?> comparer)
        {
            if(FullAnalysis)
            {
                List<NetworkService> disregardedSvcObjects = [.. allModSvcObjects.Except(allProdSvcObjects, comparer)];
                List<NetworkService> surplusSvcObjects = [.. allProdSvcObjects.Except(allModSvcObjects, comparer)];
                foreach (var svc in surplusSvcObjects)
                {
                    NetworkService? exSvc = prodServices.FirstOrDefault(n => n.Id == svc.Id && n.DestinationPort == svc.DestinationPort);
                    if(exSvc != null)
                    {
                        exSvc.IsSurplus = true;
                    }
                }
                disregardedServices.AddRange(disregardedSvcObjects);
                return disregardedSvcObjects.Count == 0 && surplusSvcObjects.Count == 0;
            }

            return allModSvcObjects.Count == allProdSvcObjects.Count
                && allModSvcObjects.Except(allProdSvcObjects, comparer).ToList().Count == 0
                && allProdSvcObjects.Except(allModSvcObjects, comparer).ToList().Count == 0;
        }
    }
}
