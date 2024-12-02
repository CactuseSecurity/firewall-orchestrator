using FWO.Api.Data;

namespace FWO.Report
{
    public class OwnerReport : ConnectionReport
    {
        public List<ModellingConnection> Connections { get; set; } = [];
        public List<ModellingConnection> RegularConnections { get; set; } = [];
        public List<ModellingConnection> Interfaces { get; set; } = [];
        public List<ModellingConnection> CommonServices { get; set; } = [];
        private readonly long DummyARid = -1;

        public OwnerReport()
        {}

        public OwnerReport(long dummyARid)
        {
            DummyARid = dummyARid;
        }

        public OwnerReport(OwnerReport report): base(report)
        {
            Connections = report.Connections;
            RegularConnections = report.RegularConnections;
            Interfaces = report.Interfaces;
            CommonServices = report.CommonServices;
            DummyARid = report.DummyARid;
        }


        public void PrepareObjectData()
        {
            AllObjects = GetAllNetworkObjects(true);
            SetObjectNumbers(ref AllObjects);
            AllServices = GetAllServices(true);
            SetSvcNumbers(ref AllServices);
        }

        public List<NetworkObject> GetAllNetworkObjects(bool resolved = false)
        {
            return GetAllNetworkObjects(Connections, resolved, DummyARid);
        }

        public List<NetworkService> GetAllServices(bool resolved = false)
        {
            return GetAllServices(Connections, resolved);
        }
    }
}
