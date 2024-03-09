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

        public void AssignConnectionNumbers()
        {
            int connNumber = 1;
            foreach (var conn in Connections)
            {
                conn.OrderNumber = connNumber++;
            }
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
            List<NetworkObject> allObjects = new();
            foreach(var conn in Connections)
            {
                List<NetworkObject> objList = new();
                foreach (var objGrp in conn.SourceAppRoles)
                {
                    objList.Add(objGrp.Content.ToNetworkObjectGroup());
                }
                foreach (var objGrp in conn.DestinationAppRoles)
                {
                    objList.Add(objGrp.Content.ToNetworkObjectGroup());
                }
                allObjects = allObjects.Union(objList).ToList();
                allObjects = allObjects.Union(Array.ConvertAll(GetAllAppServers().ToArray(), x => ModellingAppServer.ToNetworkObject(x)).ToList()).ToList();
            }
            return allObjects;

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
