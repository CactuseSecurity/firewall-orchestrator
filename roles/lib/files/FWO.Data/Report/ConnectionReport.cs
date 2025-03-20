using FWO.Data.Modelling;

namespace FWO.Data.Report
{
    public class ConnectionReport
    {
        public string Name = "";

        public List<NetworkObject> AllObjects = [];
        public List<NetworkService> AllServices = [];

        public ConnectionReport()
        {}

        public ConnectionReport(ConnectionReport report)
        {
            Name = report.Name;
            AllObjects = report.AllObjects;
            AllServices = report.AllServices;
        }

        public static void AssignConnectionNumbers(List<ModellingConnection> connections)
        {
            int connNumber = 1;
            foreach (var conn in connections)
            {
                conn.OrderNumber = connNumber++;
            }
        }

        protected static void SetSvcNumbers(ref List<NetworkService> svcList)
        {
            long number = 1;
            foreach(var svc in svcList)
            {
                svc.Number = number++;
            }
        }

        protected static void SetObjectNumbers(ref List<NetworkObject> objList)
        {
            long number = 1;
            foreach(var obj in objList)
            {
                obj.Number = number++;
            }
        }

        protected static List<NetworkService> GetAllServices(List<ModellingConnection> connections, bool resolved = false)
        {
            List<NetworkService> allServices = [];
            foreach(var conn in connections)
            {
                List<NetworkService> svcList = [];
                foreach (var svcGrp in conn.ServiceGroups)
                {
                    NetworkService serviceGroup = svcGrp.Content.ToNetworkServiceGroup();
                    svcList.Add(svcGrp.Content.ToNetworkServiceGroup());
                    if(resolved)
                    {
                        foreach(var svc in serviceGroup.ServiceGroups)
                        {
                            if(svc.Object != null)
                            {
                                svcList.Add(svc.Object);
                            }
                        }
                    }
                }
                allServices = allServices.Union(svcList).ToList();
                allServices = allServices.Union(ModellingServiceWrapper.ResolveAsNetworkServices(conn.Services).ToList()).ToList();
            }
            return allServices;
        }

        protected static List<NetworkObject> GetAllNetworkObjects(List<ModellingConnection> connections, bool resolved = false, long dummyARid = 0)
        {
            List<NetworkObject> allObjects = [];
            foreach(var conn in connections)
            {
                allObjects = allObjects.Union(GetAllNwGrpObjectsFromConn(conn, resolved, dummyARid)).ToList();
            }
            allObjects = allObjects.Union(GetAllAppServers(connections).ConvertAll(ModellingAppServer.ToNetworkObject)).ToList();
            return allObjects;
        }

        public long ResolveObjId(ModellingNwObject networkObject)
        {
            return AllObjects.FirstOrDefault(x => x.Name.StartsWith(networkObject.Name))?.Id ?? 0;
        }

        public long ResolveSvcId(ModellingSvcObject serviceObject)
        {
            return AllServices.FirstOrDefault(x => x.Name == serviceObject.Name)?.Id ?? 0;
        }

        private static List<ModellingAppServer> GetAllAppServers(List<ModellingConnection> connections)
        {
            List<ModellingAppServer> allAppServers = [];
            foreach(var conn in connections)
            {
                allAppServers = allAppServers.Union([.. ModellingAppServerWrapper.Resolve(conn.SourceAppServers)]).ToList();
                allAppServers = allAppServers.Union([.. ModellingAppServerWrapper.Resolve(conn.DestinationAppServers)]).ToList();
            }
            return allAppServers;
        }

        private static List<NetworkObject> GetAllNwGrpObjectsFromConn(ModellingConnection conn, bool resolved = false, long dummyARid = 0)
        {
            List<NetworkObject> objList = [];
            GetObjectsFromAreas(conn.SourceAreas, ref objList, resolved);
            GetObjectsFromAreas(conn.DestinationAreas, ref objList, resolved);
            GetObjectsFromAR(conn.SourceAppRoles, ref objList, resolved, dummyARid);
            GetObjectsFromAR(conn.DestinationAppRoles, ref objList, resolved, dummyARid);
            GetObjectsFromOtherGroups(conn.SourceOtherGroups, ref objList, resolved);
            GetObjectsFromOtherGroups(conn.DestinationOtherGroups, ref objList, resolved);
            return objList;
        }

        private static void GetObjectsFromAreas(List<ModellingNetworkAreaWrapper> areas, ref List<NetworkObject> objectList, bool resolved = false)
        {
            foreach (var areaWrapper in areas)
            {
                objectList.Add(areaWrapper.Content.ToNetworkObjectGroup());
                if(resolved)
                {
                    foreach(var obj in areaWrapper.Content.ToNetworkObjectGroup().ObjectGroups)
                    {
                        if(obj.Object != null)
                        {
                            objectList.Add(obj.Object);
                        }
                    }
                }
            }
        }

        private static void GetObjectsFromOtherGroups(List<ModellingNwGroupWrapper> nwGroups, ref List<NetworkObject> objectList, bool resolved = false)
        {
            foreach (var nwGrpWrapper in nwGroups)
            {
                objectList.Add(nwGrpWrapper.Content.ToNetworkObjectGroup());
                if(resolved)
                {
                    foreach(var obj in nwGrpWrapper.Content.ToNetworkObjectGroup().ObjectGroups)
                    {
                        if(obj.Object != null)
                        {
                            objectList.Add(obj.Object);
                        }
                    }
                }
            }
        }

        private static void GetObjectsFromAR(List<ModellingAppRoleWrapper> appRoles, ref List<NetworkObject> objectList, bool resolved = false, long dummyARid = 0)
        {
            foreach (var aRWrapper in appRoles.Where(a => a.Content.Id != dummyARid))
            {
                objectList.Add(aRWrapper.Content.ToNetworkObjectGroup());
                if(resolved)
                {
                    foreach(var obj in aRWrapper.Content.ToNetworkObjectGroup().ObjectGroups)
                    {
                        if(obj.Object != null)
                        {
                            objectList.Add(obj.Object);
                        }
                    }
                }
            }
        }
    }
}
