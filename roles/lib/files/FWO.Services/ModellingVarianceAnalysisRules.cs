using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Logging;
using Newtonsoft.Json;
using System.Text.Json.Serialization;

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

        private void AnalyseRules(ModellingConnection conn)
        {
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
            return !rule.IsDropRule() && !rule.Disabled
                && IsNwImplementation(rule.Froms, conn.SourceAppServers, conn.SourceAppRoles, conn.SourceAreas, conn.SourceOtherGroups)
                && IsNwImplementation(rule.Tos, conn.DestinationAppServers, conn.DestinationAppRoles, conn.DestinationAreas, conn.DestinationOtherGroups)
                && IsSvcImplementation(rule.Services, conn.Services, conn.ServiceGroups);
        }

        private bool IsNwImplementation(NetworkLocation[] networkLocations, List<ModellingAppServerWrapper> appServers,
            List<ModellingAppRoleWrapper> appRoles, List<ModellingNetworkAreaWrapper> areas, List<ModellingNwGroupWrapper> otherGroups)
        {
            if (!CompareNwAreas(networkLocations, areas))
            {
                return false;
            }
            List<NetworkObject> allProdNwObjects = networkLocations.Where(n => n.Object.Type.Name != ObjectType.Group).ToList().ConvertAll(n => n.Object);
            List<NetworkObject> allModNwObjects = ModellingAppServerWrapper.Resolve(appServers).ToList().ConvertAll(a => ModellingAppServer.ToNetworkObject(a));
            if(ruleRecognitionOption.NwResolveGroup)
            {
                foreach(var nwGroup in networkLocations.Where(n => n.Object.Type.Name == ObjectType.Group && !IsArea(n.Object)))
                {
                    allProdNwObjects.AddRange(nwGroup.Object.ObjectGroupFlats.ToList().ConvertAll(g => g.Object).ToList());
                }
                foreach(var appRole in ModellingAppRoleWrapper.Resolve(appRoles))
                {
                    allModNwObjects.AddRange(ModellingAppServerWrapper.Resolve(appRole.AppServers).ToList().ConvertAll(a => ModellingAppServer.ToNetworkObject(a)));
                }
            }

            if(allModNwObjects.Count != allProdNwObjects.Count
                || allModNwObjects.Except(allProdNwObjects, networkObjectComparer).ToList().Count > 0 
                || allProdNwObjects.Except(allModNwObjects, networkObjectComparer).ToList().Count > 0)
            {
                return false;
            }
            return ruleRecognitionOption.NwResolveGroup ? true : CompareNwGroups(networkLocations, appRoles, otherGroups);
        }

        private bool CompareNwAreas(NetworkLocation[] networkLocations, List<ModellingNetworkAreaWrapper> areas)
        {
            List<NetworkObject> allProdNwAreas = networkLocations.Where(n => n.Object.Type.Name == ObjectType.Group && IsArea(n.Object)).ToList().ConvertAll(n => n.Object);
            List<NetworkObject> allModNwAreas = ModellingNetworkAreaWrapper.Resolve(areas).ToList().ConvertAll(a => a.ToNetworkObjectGroup(true));
            NetworkObjectComparer networkAreaComparer = new(new(){ NwRegardName = true, NwRegardIp = false });

            return allModNwAreas.Count == allProdNwAreas.Count
                && allModNwAreas.Except(allProdNwAreas, networkAreaComparer).ToList().Count == 0
                && allProdNwAreas.Except(allModNwAreas, networkAreaComparer).ToList().Count == 0;
        }

        private bool IsArea(NetworkObject nwGroup)
        {
            return nwGroup.Name.StartsWith(namingConvention.NetworkAreaPattern);
        }

        private bool CompareNwGroups(NetworkLocation[] networkLocations, List<ModellingAppRoleWrapper> appRoles, List<ModellingNwGroupWrapper> otherGroups)
        {
            List<NetworkObject> allProdNwGroups = networkLocations.Where(n => n.Object.Type.Name == ObjectType.Group && !IsArea(n.Object)).ToList().ConvertAll(n => n.Object);
            List<NetworkObject> allModNwGroups = ModellingAppRoleWrapper.Resolve(appRoles).ToList().ConvertAll(a => a.ToNetworkObjectGroup());
            allModNwGroups.AddRange(ModellingNwGroupWrapper.Resolve(otherGroups).ToList().ConvertAll(a => a.ToNetworkObjectGroup()));

            return allModNwGroups.Count == allProdNwGroups.Count
                && allModNwGroups.Except(allProdNwGroups, networkObjectGroupComparer).ToList().Count == 0
                && allProdNwGroups.Except(allModNwGroups, networkObjectGroupComparer).ToList().Count == 0;
        }

        private bool IsSvcImplementation(ServiceWrapper[] networkServices, List<ModellingServiceWrapper> services, List<ModellingServiceGroupWrapper> serviceGroups)
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
            if( allModServices.Count != allProdServices.Count
                || allModServices.Except(allProdServices, networkServiceComparer).ToList().Count > 0
                || allProdServices.Except(allModServices, networkServiceComparer).ToList().Count > 0)
            {
                return false;
            }
            return ruleRecognitionOption.SvcResolveGroup ? true : CompareSvcGroups(networkServices, serviceGroups);
        }

        private bool CompareSvcGroups(ServiceWrapper[] networkServices, List<ModellingServiceGroupWrapper> serviceGroups)
        {
            List<NetworkService> allProdSvcGroups = networkServices.Where(n => n.Content.Type.Name == ServiceType.Group).ToList().ConvertAll(s => s.Content).ToList();
            List<NetworkService> allModSvcGroups = ModellingServiceGroupWrapper.Resolve(serviceGroups).ToList().ConvertAll(a => a.ToNetworkServiceGroup());

            return allModSvcGroups.Count == allProdSvcGroups.Count
                && allModSvcGroups.Except(allProdSvcGroups, networkServiceGroupComparer).ToList().Count == 0
                && allProdSvcGroups.Except(allModSvcGroups, networkServiceGroupComparer).ToList().Count == 0;
        }
    }
}
