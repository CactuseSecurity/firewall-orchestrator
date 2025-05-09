using FWO.Data.Modelling;

namespace FWO.Data.Report
{
    public class OwnerReport : ConnectionReport
    {
        public FwoOwner Owner { get; set; } = new();
        public List<ModellingConnection> Connections { get; set; } = [];
        public List<ModellingConnection> RegularConnections { get; set; } = [];
        public List<ModellingConnection> Interfaces { get; set; } = [];
        public List<ModellingConnection> CommonServices { get; set; } = [];
        public List<ManagementReport> ManagementData { get; set; } = [];
        public List<ModProdDifference> RuleDifferences { get; set; } = [];
        private readonly long DummyARid = -1;
        public int ModelledConnectionsCount { get; set; }

        public Dictionary<int, List<ModellingAppRole>> MissingAppRoles { get; set; } = [];
        public Dictionary<int, List<ModellingAppRole>> DifferingAppRoles { get; set; } = [];
        public AppRoleStats AppRoleStats { get; set; } = new();

        public OwnerReport()
        {}

        public OwnerReport(long dummyARid)
        {
            DummyARid = dummyARid;
        }

        public OwnerReport(OwnerReport report): base(report)
        {
            Owner = report.Owner;
            Connections = report.Connections;
            RegularConnections = report.RegularConnections;
            Interfaces = report.Interfaces;
            CommonServices = report.CommonServices;
            ManagementData = report.ManagementData;
            RuleDifferences = report.RuleDifferences;
            DummyARid = report.DummyARid;
            ModelledConnectionsCount = report.ModelledConnectionsCount;
            AppRoleStats = report.AppRoleStats;
        }

        public override List<NetworkObject> GetAllNetworkObjects(bool resolved = false, bool resolveNetworkAreas = false)
        {
            return GetAllNetworkObjects(Connections, resolved, resolveNetworkAreas, DummyARid);
        }

        public override List<NetworkService> GetAllServices(bool resolved = false)
        {
            return GetAllServices(Connections, resolved);
        }

        public void ExtractConnectionsToAnalyse()
        {
            Connections = [.. Connections.Where(x => !(x.IsInterface ||
                x.GetBoolProperty(ConState.InterfaceRequested.ToString()) ||
                x.GetBoolProperty(ConState.InterfaceRejected.ToString()) || 
                x.EmptyAppRolesFound(DummyARid) ||
                x.DeletedObjectsFound()
                )).OrderByDescending(y => y.IsCommonService)];
            ModelledConnectionsCount = Connections.Count;
        }
    }
}
