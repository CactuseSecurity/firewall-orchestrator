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


        public void PrepareObjectData()
        {
            AllObjects = GetAllNetworkObjects(true);
            SetObjectNumbers(ref AllObjects);
            AllServices = GetAllServices(true);
            SetSvcNumbers(ref AllServices);
        }

        public List<NetworkObject> GetAllNetworkObjects(bool resolved = false)
        {
            return GetAllNetworkObjects(GlobalComSvcs, resolved);
        }

        public List<NetworkService> GetAllServices(bool resolved = false)
        {
            return GetAllServices(GlobalComSvcs, resolved);
        }
    }
}
