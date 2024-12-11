using FWO.Api.Data;

namespace FWO.Report
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

        public List<string> GetLinkedSrcNames(ModellingConnection conn, int chapterNumber)
        {
            List<string> names = ModellingNetworkAreaWrapper.Resolve(conn.SourceAreas).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, chapterNumber, ResolveObjId(s)));
            names.AddRange(ModellingNwGroupWrapper.Resolve(conn.SourceOtherGroups).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, chapterNumber, ResolveObjId(s))));
            names.AddRange(ModellingAppRoleWrapper.Resolve(conn.SourceAppRoles).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, chapterNumber, ResolveObjId(s))));
            names.AddRange(ModellingAppServerWrapper.Resolve(conn.SourceAppServers).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, chapterNumber, ResolveObjId(s))));
            return names;
        }
        
        public List<string> GetLinkedDstNames(ModellingConnection conn, int chapterNumber)
        {
            List<string> names = ModellingNetworkAreaWrapper.Resolve(conn.DestinationAreas).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, chapterNumber, ResolveObjId(s)));
            names.AddRange(ModellingNwGroupWrapper.Resolve(conn.DestinationOtherGroups).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, chapterNumber, ResolveObjId(s))));
            names.AddRange(ModellingAppRoleWrapper.Resolve(conn.DestinationAppRoles).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, chapterNumber, ResolveObjId(s))));
            names.AddRange(ModellingAppServerWrapper.Resolve(conn.DestinationAppServers).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, chapterNumber, ResolveObjId(s))));
            return names;
        }

        public List<string> GetLinkedSvcNames(ModellingConnection conn, int chapterNumber)
        {
            List<string> names = ModellingServiceGroupWrapper.Resolve(conn.ServiceGroups).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.Svc, chapterNumber, ResolveSvcId(s)));
            names.AddRange(ModellingServiceWrapper.Resolve(conn.Services).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.Svc, chapterNumber, ResolveSvcId(s))));
            return names;
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

        private long ResolveObjId(ModellingNwObject networkObject)
        {
            return AllObjects.FirstOrDefault(x => x.Name.StartsWith(networkObject.Name))?.Id ?? 0;
        }

        private long ResolveSvcId(ModellingSvcObject serviceObject)
        {
            return AllServices.FirstOrDefault(x => x.Name == serviceObject.Name)?.Id ?? 0;
        }

        private static string ConstructOutput(ModellingObject inputObj, string type, int chapterNumber, long objId)
        {
            return ReportBase.ConstructLink(type, "", chapterNumber, objId, inputObj.Display(), OutputLocation.export, $"a{inputObj.AppId}", "");
        }

        // public static List<string> GetSrcNames(ModellingConnection conn)
        // {
        //     List<string> names = ModellingNetworkAreaWrapper.Resolve(conn.SourceAreas).ToList().ConvertAll(s => s.DisplayHtml());
        //     names.AddRange(ModellingNwGroupWrapper.Resolve(conn.SourceOtherGroups).ToList().ConvertAll(s => s.DisplayHtml()));
        //     names.AddRange(ModellingAppRoleWrapper.Resolve(conn.SourceAppRoles).ToList().ConvertAll(s => s.DisplayHtml()));
        //     names.AddRange(ModellingAppServerWrapper.Resolve(conn.SourceAppServers).ToList().ConvertAll(s => s.DisplayHtml()));
        //     return names;
        // }

        // public static List<string> GetDstNames(ModellingConnection conn)
        // {
        //     List<string> names = ModellingNetworkAreaWrapper.Resolve(conn.DestinationAreas).ToList().ConvertAll(s => s.DisplayHtml());
        //     names.AddRange(ModellingNwGroupWrapper.Resolve(conn.DestinationOtherGroups).ToList().ConvertAll(s => s.DisplayHtml()));
        //     names.AddRange(ModellingAppRoleWrapper.Resolve(conn.DestinationAppRoles).ToList().ConvertAll(s => s.DisplayHtml()));
        //     names.AddRange(ModellingAppServerWrapper.Resolve(conn.DestinationAppServers).ToList().ConvertAll(s => s.DisplayHtml()));
        //     return names;
        // }

        // public static List<string> GetSvcNames(ModellingConnection conn)
        // {
        //     List<string> names = ModellingServiceGroupWrapper.Resolve(conn.ServiceGroups).ToList().ConvertAll(s => s.DisplayHtml());
        //     names.AddRange(ModellingServiceWrapper.Resolve(conn.Services).ToList().ConvertAll(s => s.DisplayHtml()));
        //     return names;
        // }
    }
}
