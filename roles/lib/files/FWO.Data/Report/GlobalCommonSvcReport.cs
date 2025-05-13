using FWO.Data.Modelling;

namespace FWO.Data.Report
{
    public class GlobalCommonSvcReport : ConnectionReport
    {
        public List<ModellingConnection> GlobalComSvcs = [];

        public GlobalCommonSvcReport() : base()
        {}

        public GlobalCommonSvcReport(GlobalCommonSvcReport report) : base(report)
        {
            GlobalComSvcs = report.GlobalComSvcs;
        }

        public override List<NetworkObject> GetAllNetworkObjects(bool resolved = false, bool resolveNetworkAreas = false)
        {
            return GetAllNetworkObjects(GlobalComSvcs, resolved, resolveNetworkAreas);
        }

        public override List<NetworkService> GetAllServices(bool resolved = false)
        {
            return GetAllServices(GlobalComSvcs, resolved);
        }
    }
}
