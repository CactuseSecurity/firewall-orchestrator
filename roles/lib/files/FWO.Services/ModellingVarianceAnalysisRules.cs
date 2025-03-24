using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Logging;

namespace FWO.Services
{
    /// <summary>
	/// Part of Variance Analysis Class analysing the rules
	/// </summary>
    public partial class ModellingVarianceAnalysis
    {
        readonly NetworkObjectComparer networkObjectComparer = new();
        readonly NetworkObjectGroupComparer networkObjectGroupComparer = new();
        readonly NetworkServiceComparer networkServiceComparer = new();

        private void AnalyseRules(ModellingConnection conn)
        {
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
                if(Equals(rule, conn))
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

        private bool Equals(Rule rule, ModellingConnection conn)
        {
            return !rule.IsDropRule() && !rule.Disabled
                && Equals(rule.Froms, conn.SourceAppServers, conn.SourceAppRoles, conn.SourceAreas, conn.SourceOtherGroups)
                && Equals(rule.Tos, conn.DestinationAppServers, conn.DestinationAppRoles, conn.DestinationAreas, conn.DestinationOtherGroups)
                && Equals(rule.Services, conn.Services, conn.ServiceGroups);
        }

        private bool Equals(NetworkLocation[] networkLocations, List<ModellingAppServerWrapper> appServers,
            List<ModellingAppRoleWrapper> appRoles, List<ModellingNetworkAreaWrapper> areas, List<ModellingNwGroupWrapper> otherGroups)
        {
            List<NetworkObject> allProdNwObjects = networkLocations.Where(n => n.Object.Type.Name == ObjectType.Group).ToList().ConvertAll(n => n.Object);
            List<NetworkObject> allModNwObjects = ModellingAppServerWrapper.Resolve(appServers).ToList().ConvertAll(s => ModellingAppServer.ToNetworkObject(s));

            List<NetworkObject> modNotProdObj = allModNwObjects.Except(allProdNwObjects, networkObjectGroupComparer).ToList();
            List<NetworkObject> prodNotModObj = allProdNwObjects.Except(allModNwObjects, networkObjectGroupComparer).ToList();

            if(modNotProdObj.Count > 0 && prodNotModObj.Count > 0)
            {
                return false;
            }

            List<NetworkObject> allProdNwGroups = networkLocations.Where(n => n.Object.Type.Name == ObjectType.Group).ToList().ConvertAll(n => n.Object);
            List<NetworkObject> allModNwGroups = ModellingAppRoleWrapper.Resolve(appRoles).ToList().ConvertAll(a => a.ToNetworkObjectGroup());
            allModNwGroups.AddRange(ModellingNetworkAreaWrapper.Resolve(areas).ToList().ConvertAll(a => a.ToNetworkObjectGroup()));
            allModNwGroups.AddRange(ModellingNwGroupWrapper.Resolve(otherGroups).ToList().ConvertAll(a => a.ToNetworkObjectGroup()));

            List<NetworkObject> modNotProdGrp = allModNwGroups.Except(allProdNwGroups, networkObjectGroupComparer).ToList();
            List<NetworkObject> prodNotModGrp = allProdNwGroups.Except(allModNwGroups, networkObjectGroupComparer).ToList();

            return modNotProdGrp.Count == 0 && prodNotModGrp.Count == 0;
        }

        private bool Equals(ServiceWrapper[] networkServices, List<ModellingServiceWrapper> services, List<ModellingServiceGroupWrapper> serviceGroups)
        {
            List<NetworkService> allProdServices = networkServices.ToList().ConvertAll(s => s.Content).ToList();
            List<NetworkService> allModServices = ModellingServiceWrapper.Resolve(services).ToList().ConvertAll(s => ModellingService.ToNetworkService(s));
            foreach(var svcGrp in ModellingServiceGroupWrapper.Resolve(serviceGroups))
            {
                allModServices.AddRange(ModellingServiceWrapper.Resolve(svcGrp.Services).ToList().ConvertAll(s => ModellingService.ToNetworkService(s)));
            }

            List<NetworkService> modNotProd = allModServices.Except(allProdServices, networkServiceComparer).ToList();
            List<NetworkService> prodNotMod = allProdServices.Except(allModServices, networkServiceComparer).ToList();

            return modNotProd.Count == 0 && prodNotMod.Count == 0;
        }
    }
}
