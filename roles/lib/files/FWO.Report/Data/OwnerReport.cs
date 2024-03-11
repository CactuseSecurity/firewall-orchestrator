using FWO.Api.Data;

namespace FWO.Report
{
    public class OwnerReport
    {
        public string Name = "";
        public List<ModellingConnection> Connections { get; set; } = new ();
        public List<ModellingConnection> RegularConnections { get; set; } = new ();
        public List<ModellingConnection> Interfaces { get; set; } = new ();
        public List<ModellingConnection> CommonServices { get; set; } = new ();

        public OwnerReport()
        {}

        public OwnerReport(OwnerReport report)
        {
            Name = report.Name;
            Connections = new (report.Connections);
        }

        public static void AssignConnectionNumbers(List<ModellingConnection> connections)
        {
            int connNumber = 1;
            foreach (var conn in connections)
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
            }
            allObjects = allObjects.Union(Array.ConvertAll(GetAllAppServers().ToArray(), x => ModellingAppServer.ToNetworkObject(x)).ToList()).ToList();
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

        public static List<string> GetSrcNames(ModellingConnection conn)
        {
            List<string> names = ModellingNwGroupWrapper.Resolve(conn.SourceNwGroups).ToList().ConvertAll(s => s.DisplayHtml());
            names.AddRange(ModellingAppRoleWrapper.Resolve(conn.SourceAppRoles).ToList().ConvertAll(s => s.DisplayHtml()));
            names.AddRange(ModellingAppServerWrapper.Resolve(conn.SourceAppServers).ToList().ConvertAll(s => s.DisplayHtml()));
            return names;
        }
        
        public static List<string> GetDstNames(ModellingConnection conn)
        {
            List<string> names = ModellingNwGroupWrapper.Resolve(conn.DestinationNwGroups).ToList().ConvertAll(s => s.DisplayHtml());
            names.AddRange(ModellingAppRoleWrapper.Resolve(conn.DestinationAppRoles).ToList().ConvertAll(s => s.DisplayHtml()));
            names.AddRange(ModellingAppServerWrapper.Resolve(conn.DestinationAppServers).ToList().ConvertAll(s => s.DisplayHtml()));
            return names;
        }

        public static List<string> GetSvcNames(ModellingConnection conn)
        {
            List<string> names = ModellingServiceGroupWrapper.Resolve(conn.ServiceGroups).ToList().ConvertAll(s => s.DisplayHtml());
            names.AddRange(ModellingServiceWrapper.Resolve(conn.Services).ToList().ConvertAll(s => s.DisplayHtml()));
            return names;
        }
    }
}
