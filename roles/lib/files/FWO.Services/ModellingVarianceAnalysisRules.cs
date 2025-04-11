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
            if(FullAnalysis)
            {
                List<NetworkLocation> disregardedFroms = [];
                List<NetworkLocation> disregardedTos = [];
                List<NetworkService> disregardedServices = [];
                isImpl &= IsNwImplementation(rule.Froms, conn.SourceAppServers, conn.SourceAppRoles, conn.SourceAreas, conn.SourceOtherGroups, disregardedFroms);
                isImpl &= IsNwImplementation(rule.Tos, conn.DestinationAppServers, conn.DestinationAppRoles, conn.DestinationAreas, conn.DestinationOtherGroups, disregardedTos);
                isImpl &= IsSvcImplementation(rule.Services, conn.Services, conn.ServiceGroups, disregardedServices);
                rule.DisregardedFroms = disregardedFroms.ToArray();
                rule.DisregardedTos = disregardedTos.ToArray();
                rule.DisregardedServices = disregardedServices.ToArray();
            }
            else if (isImpl)
            {
                isImpl = IsNwImplementation(rule.Froms, conn.SourceAppServers, conn.SourceAppRoles, conn.SourceAreas, conn.SourceOtherGroups, [])
                    && IsNwImplementation(rule.Tos, conn.DestinationAppServers, conn.DestinationAppRoles, conn.DestinationAreas, conn.DestinationOtherGroups, [])
                    && IsSvcImplementation(rule.Services, conn.Services, conn.ServiceGroups, []);
            }
            return isImpl;
        }

        private bool IsNwImplementation(NetworkLocation[] networkLocations, List<ModellingAppServerWrapper> appServers,
            List<ModellingAppRoleWrapper> appRoles, List<ModellingNetworkAreaWrapper> areas, List<ModellingNwGroupWrapper> otherGroups, List<NetworkLocation> disregardedLocations)
        {
            bool isImpl = true;
            if (!CompareNwAreas(networkLocations, areas, disregardedLocations))
            {
                if(!FullAnalysis)
                {
                    return false;
                }
                isImpl = false;
            }
            if (!CompareAppServers(networkLocations, appServers, appRoles, disregardedLocations))
            {
                if(!FullAnalysis)
                {
                    return false;
                }
                isImpl = false;
            }
            return (ruleRecognitionOption.NwResolveGroup ? true : CompareRemainingNwGroups(networkLocations, appRoles, otherGroups, disregardedLocations)) && isImpl;
        }

        private bool CompareNwAreas(NetworkLocation[] networkLocations, List<ModellingNetworkAreaWrapper> areas, List<NetworkLocation> disregardedLocations)
        {
            List<NetworkObject> allProdNwAreas = networkLocations.Where(n => n.Object.Type.Name == ObjectType.Group && IsArea(n.Object)).ToList().ConvertAll(n => n.Object);
            List<NetworkObject> allModNwAreas = ModellingNetworkAreaWrapper.Resolve(areas).ToList().ConvertAll(a => a.ToNetworkObjectGroup(true));
            NetworkObjectComparer networkAreaComparer = new(new(){ NwRegardName = true, NwRegardIp = false });
            return CompareNwObjects(allModNwAreas, allProdNwAreas, networkLocations, disregardedLocations, networkAreaComparer);
        }

        private bool IsArea(NetworkObject nwGroup)
        {
            return nwGroup.Name.StartsWith(namingConvention.NetworkAreaPattern);
        }

        private bool CompareAppServers(NetworkLocation[] networkLocations, List<ModellingAppServerWrapper> appServers,
            List<ModellingAppRoleWrapper> appRoles, List<NetworkLocation> disregardedLocations)
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
            return CompareNwObjects(allModNwObjects, allProdNwObjects, networkLocations, disregardedLocations, networkObjectComparer);
        }

        private bool CompareRemainingNwGroups(NetworkLocation[] networkLocations, List<ModellingAppRoleWrapper> appRoles, List<ModellingNwGroupWrapper> otherGroups, List<NetworkLocation> disregardedLocations)
        {
            List<NetworkObject> allProdNwGroups = networkLocations.Where(n => n.Object.Type.Name == ObjectType.Group && !IsArea(n.Object)).ToList().ConvertAll(n => n.Object);
            List<NetworkObject> allModNwGroups = ModellingAppRoleWrapper.Resolve(appRoles).ToList().ConvertAll(a => a.ToNetworkObjectGroup());
            allModNwGroups.AddRange(ModellingNwGroupWrapper.Resolve(otherGroups).ToList().ConvertAll(a => a.ToNetworkObjectGroup()));
            return CompareNwObjects(allModNwGroups, allProdNwGroups, networkLocations, disregardedLocations, networkObjectGroupComparer);
        }

        private bool CompareNwObjects(List<NetworkObject> allModGroups, List<NetworkObject> allProdGroups, NetworkLocation[] networkLocations,
            List<NetworkLocation> disregardedLocations, IEqualityComparer<NetworkObject?> comparer)
        {
            if(FullAnalysis)
            {
                List<NetworkObject> disregardedGroups = allModGroups.Except(allProdGroups, comparer).ToList();
                List<NetworkObject> surplusGroups = allProdGroups.Except(allModGroups, comparer).ToList();
                foreach (var obj in surplusGroups)
                {
                    networkLocations.FirstOrDefault(n => n.Object.Id == obj.Id).Object.IsSurplus = true;
                }
                disregardedLocations.AddRange(disregardedGroups.ConvertAll(o => new NetworkLocation(new(), o)));
                return disregardedGroups.Count == 0 && surplusGroups.Count == 0;
            }

            return allModGroups.Count == allProdGroups.Count
                && allModGroups.Except(allProdGroups, comparer).ToList().Count == 0
                && allProdGroups.Except(allModGroups, comparer).ToList().Count == 0;
        }

        private bool IsSvcImplementation(ServiceWrapper[] networkServices, List<ModellingServiceWrapper> services, List<ModellingServiceGroupWrapper> serviceGroups, List<NetworkService> disregardedServices)
        {
            bool isImpl = true;
            if(!CompareServices(networkServices, services, serviceGroups, disregardedServices))
            {
                if(!FullAnalysis)
                {
                    return false;
                }
                isImpl = false;
            }
            return (ruleRecognitionOption.SvcResolveGroup ? true : CompareSvcGroups(networkServices, serviceGroups, disregardedServices)) && isImpl;
        }

        private bool CompareServices(ServiceWrapper[] networkServices, List<ModellingServiceWrapper> services, List<ModellingServiceGroupWrapper> serviceGroups, List<NetworkService> disregardedServices)
        {
            List<NetworkService> allProdServices = networkServices.Where(s => s.Content.Type.Name != ServiceType.Group).ToList().ConvertAll(s => s.Content).ToList();
            List<NetworkService> allModServices = ModellingServiceWrapper.Resolve(services).ToList().ConvertAll(s => ModellingService.ToNetworkService(s));
            if(ruleRecognitionOption.SvcResolveGroup)
            {
                foreach(var svcGrp in ModellingServiceGroupWrapper.Resolve(serviceGroups))
                {
                    allModServices.AddRange(ModellingServiceWrapper.Resolve(svcGrp.Services).ToList().ConvertAll(s => ModellingService.ToNetworkService(s)));
                }
            }
            return CompareSvcObjects(allModServices, allProdServices, networkServices, disregardedServices, networkServiceComparer);
        }

        private bool CompareSvcGroups(ServiceWrapper[] networkServices, List<ModellingServiceGroupWrapper> serviceGroups, List<NetworkService> disregardedServices)
        {
            List<NetworkService> allProdSvcGroups = networkServices.Where(n => n.Content.Type.Name == ServiceType.Group).ToList().ConvertAll(s => s.Content).ToList();
            List<NetworkService> allModSvcGroups = ModellingServiceGroupWrapper.Resolve(serviceGroups).ToList().ConvertAll(a => a.ToNetworkServiceGroup());
            return CompareSvcObjects(allModSvcGroups, allProdSvcGroups, networkServices, disregardedServices, networkServiceGroupComparer);
        }

        private bool CompareSvcObjects(List<NetworkService> allModGroups, List<NetworkService> allProdGroups, ServiceWrapper[] networkServices,
            List<NetworkService> disregardedLocations, IEqualityComparer<NetworkService?> comparer)
        {
            if(FullAnalysis)
            {
                List<NetworkService> disregardedGroups = allModGroups.Except(allProdGroups, comparer).ToList();
                List<NetworkService> surplusGroups = allProdGroups.Except(allModGroups, comparer).ToList();
                foreach (var svc in surplusGroups)
                {
                    networkServices.FirstOrDefault(n => n.Content.Id == svc.Id).Content.IsSurplus = true;
                }
                disregardedLocations.AddRange(disregardedGroups);
                return disregardedGroups.Count == 0 && surplusGroups.Count == 0;
            }

            return allModGroups.Count == allProdGroups.Count
                && allModGroups.Except(allProdGroups, comparer).ToList().Count == 0
                && allProdGroups.Except(allModGroups, comparer).ToList().Count == 0;
        }
    }
}
