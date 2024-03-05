namespace FWO.Api.Data
{
    public class OwnerReport
    {
        public string Name = "";
        public List<ModellingConnection> Connections { get; set; } = new ();

        public OwnerReport()
        {}

        public OwnerReport(OwnerReport report)
        {
            Name = report.Name;
            Connections = new (report.Connections);
        }

        public List<ModellingAppServer> GetAllAppServers()
        {
            List<ModellingAppServer> allAppServers = new();
            foreach(var conn in Connections)
            {
                allAppServers = allAppServers.Union(ModellingAppServerWrapper.Resolve(conn.SourceAppServers).ToList()).ToList();
                allAppServers = allAppServers.Union(ModellingAppServerWrapper.Resolve(conn.DestinationAppServers).ToList()).ToList();
            }
            return allAppServers;
        }

        public List<NetworkObject> GetAllNetworkObjects()
        {
            return Array.ConvertAll(GetAllAppServers().ToArray(), x => x.ToNetworkObject()).ToList();
        }

        public List<NetworkService> GetAllServices()
        {
            List<NetworkService> allServices = new();
            foreach(var conn in Connections)
            {
                List<NetworkService> svcList = new();
                foreach (var svcGrp in conn.ServiceGroups)
                {
                    svcList.Add(svcGrp.Content.ToNetworkServiceGroup());
                }
                allServices = allServices.Union(svcList).ToList();
                allServices = allServices.Union(ModellingServiceWrapper.ResolveAsNetworkServices(conn.Services).ToList()).ToList();
            }
            return allServices;
        }
    }
}
