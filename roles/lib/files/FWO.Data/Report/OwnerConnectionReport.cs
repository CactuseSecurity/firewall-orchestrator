using FWO.Data.Modelling;

namespace FWO.Data.Report
{
    public class OwnerConnectionReport : ConnectionReport
    {
        public FwoOwner Owner { get; set; } = new();
        public List<ModellingConnection> Connections { get; set; } = [];
        public List<ModellingConnection> RegularConnections { get; set; } = [];
        public List<ModellingConnection> Interfaces { get; set; } = [];
        public List<ModellingConnection> CommonServices { get; set; } = [];
        public List<ManagementReport> UnmodelledRules { get; set; } = [];
        public List<ManagementReport> RulesForDeletedConns { get; set; } = [];
        public List<ModProdDifference> RuleDifferences { get; set; } = [];

        /// <summary>
        /// Connections the variance analysis left out, with the reason.
        /// </summary>
        public List<ModellingExcludedConnection> ExcludedConnections { get; set; } = [];

        /// <summary>
        /// Id of the dummy app role, which never counts as empty app role.
        /// </summary>
        public long DummyAppRoleId { get; } = -1;
        public int ModelledConnectionsCount { get; set; }

        public Dictionary<int, List<ModellingAppRole>> MissingAppRoles { get; set; } = [];
        public Dictionary<int, List<ModellingAppRole>> DifferingAppRoles { get; set; } = [];
        public AppRoleStats AppRoleStats { get; set; } = new();
        public string ImplementationState { get; set; } = "";


        public OwnerConnectionReport()
        { }

        public OwnerConnectionReport(long dummyARid)
        {
            DummyAppRoleId = dummyARid;
        }

        public OwnerConnectionReport(OwnerConnectionReport report) : base(report)
        {
            Owner = report.Owner;
            Connections = report.Connections;
            RegularConnections = report.RegularConnections;
            Interfaces = report.Interfaces;
            CommonServices = report.CommonServices;
            UnmodelledRules = report.UnmodelledRules;
            RulesForDeletedConns = report.RulesForDeletedConns;
            RuleDifferences = report.RuleDifferences;
            ExcludedConnections = report.ExcludedConnections;
            DummyAppRoleId = report.DummyAppRoleId;
            ModelledConnectionsCount = report.ModelledConnectionsCount;
            AppRoleStats = report.AppRoleStats;
        }

        public override List<NetworkObject> GetAllNetworkObjects(bool resolved = false, bool resolveNetworkAreas = false)
        {
            return GetAllNetworkObjects(Connections, resolved, resolveNetworkAreas, DummyAppRoleId);
        }

        public override List<NetworkService> GetAllServices(bool resolved = false)
        {
            return GetAllServices(Connections, resolved);
        }
    }
}
