using FWO.Api.Data;

namespace FWO.Report
{
    public class OwnerReport
    {
        public string Name = "";
        public List<ModellingConnection> Connections { get; set; } = [];
        public List<ModellingConnection> RegularConnections { get; set; } = [];
        public List<ModellingConnection> Interfaces { get; set; } = [];
        public List<ModellingConnection> CommonServices { get; set; } = [];

        public List<NetworkObject> AllObjects = [];
        public List<NetworkService> AllServices = [];
        private readonly long DummyARid = -1;

        public OwnerReport()
        {}

        public OwnerReport(long dummyARid)
        {
            DummyARid = dummyARid;
        }

        public OwnerReport(OwnerReport report)
        {
            Name = report.Name;
            Connections = report.Connections;
            RegularConnections = report.RegularConnections;
            Interfaces = report.Interfaces;
            CommonServices = report.CommonServices;
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

        public void PrepareObjectData()
        {
            AllObjects = GetAllNetworkObjects(true);
            SetObjectNumbers(ref AllObjects);
            AllServices = GetAllServices(true);
            SetSvcNumbers(ref AllServices);
        }

        public long ResolveObjNumber(ModellingNwObject networkObject)
        {
            return AllObjects.FirstOrDefault(x => x.Name == networkObject.Name)?.Number ?? 0;
        }

        public long ResolveSvcNumber(ModellingSvcObject serviceObject)
        {
            return AllServices.FirstOrDefault(x => x.Name == serviceObject.Name)?.Number ?? 0;
        }

        public List<ModellingAppServer> GetAllAppServers()
        {
            List<ModellingAppServer> allAppServers = [];
            foreach(var conn in Connections)
            {
                allAppServers = allAppServers.Union(ModellingAppServerWrapper.Resolve(conn.SourceAppServers).ToList()).ToList();
                allAppServers = allAppServers.Union(ModellingAppServerWrapper.Resolve(conn.DestinationAppServers).ToList()).ToList();
            }
            return allAppServers;
        }

        public List<NetworkObject> GetAllNetworkObjects(bool resolved = false)
        {
            List<NetworkObject> allObjects = [];
            foreach(var conn in Connections)
            {
                List<NetworkObject> objList = [];
                GetObjectsFromAreas(conn.SourceAreas, ref objList, resolved);
                GetObjectsFromAreas(conn.DestinationAreas, ref objList, resolved);
                GetObjectsFromAR(conn.SourceAppRoles, ref objList, resolved);
                GetObjectsFromAR(conn.DestinationAppRoles, ref objList, resolved);
                GetObjectsFromOtherGroups(conn.SourceOtherGroups, ref objList, resolved);
                GetObjectsFromOtherGroups(conn.DestinationOtherGroups, ref objList, resolved);
                allObjects = allObjects.Union(objList).ToList();
            }
            allObjects = allObjects.Union(GetAllAppServers().ConvertAll(ModellingAppServer.ToNetworkObject)).ToList();
            return allObjects;
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

        private void GetObjectsFromAR(List<ModellingAppRoleWrapper> appRoles, ref List<NetworkObject> objectList, bool resolved = false)
        {
            foreach (var aRWrapper in appRoles.Where(a => a.Content.Id != DummyARid))
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

        public List<NetworkService> GetAllServices(bool resolved = false)
        {
            List<NetworkService> allServices = [];
            foreach(var conn in Connections)
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

        public static List<string> GetSrcNames(ModellingConnection conn)
        {
            List<string> names = ModellingNetworkAreaWrapper.Resolve(conn.SourceAreas).ToList().ConvertAll(s => s.DisplayHtml());
            names.AddRange(ModellingNwGroupWrapper.Resolve(conn.SourceOtherGroups).ToList().ConvertAll(s => s.DisplayHtml()));
            names.AddRange(ModellingAppRoleWrapper.Resolve(conn.SourceAppRoles).ToList().ConvertAll(s => s.DisplayHtml()));
            names.AddRange(ModellingAppServerWrapper.Resolve(conn.SourceAppServers).ToList().ConvertAll(s => s.DisplayHtml()));
            return names;
        }

        public static List<string> GetDstNames(ModellingConnection conn)
        {
            List<string> names = ModellingNetworkAreaWrapper.Resolve(conn.DestinationAreas).ToList().ConvertAll(s => s.DisplayHtml());
            names.AddRange(ModellingNwGroupWrapper.Resolve(conn.DestinationOtherGroups).ToList().ConvertAll(s => s.DisplayHtml()));
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

        public List<string> GetLinkedSrcNames(ModellingConnection conn)
        {
            List<string> names = ModellingNetworkAreaWrapper.Resolve(conn.SourceAreas).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, ResolveObjNumber(s)));
            names.AddRange(ModellingNwGroupWrapper.Resolve(conn.SourceOtherGroups).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, ResolveObjNumber(s))));
            names.AddRange(ModellingAppRoleWrapper.Resolve(conn.SourceAppRoles).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, ResolveObjNumber(s))));
            names.AddRange(ModellingAppServerWrapper.Resolve(conn.SourceAppServers).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, ResolveObjNumber(s))));
            return names;
        }
        
        public List<string> GetLinkedDstNames(ModellingConnection conn)
        {
            List<string> names = ModellingNetworkAreaWrapper.Resolve(conn.DestinationAreas).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, ResolveObjNumber(s)));
            names.AddRange(ModellingNwGroupWrapper.Resolve(conn.DestinationOtherGroups).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, ResolveObjNumber(s))));
            names.AddRange(ModellingAppRoleWrapper.Resolve(conn.DestinationAppRoles).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, ResolveObjNumber(s))));
            names.AddRange(ModellingAppServerWrapper.Resolve(conn.DestinationAppServers).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.NwObj, ResolveObjNumber(s))));
            return names;
        }

        public List<string> GetLinkedSvcNames(ModellingConnection conn)
        {
            List<string> names = ModellingServiceGroupWrapper.Resolve(conn.ServiceGroups).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.Svc, ResolveSvcNumber(s)));
            names.AddRange(ModellingServiceWrapper.Resolve(conn.Services).ToList().ConvertAll(s => ConstructOutput(s, ObjCatString.Svc, ResolveSvcNumber(s))));
            return names;
        }

        private static void SetSvcNumbers(ref List<NetworkService> svcList)
        {
            long number = 1;
            foreach(var svc in svcList)
            {
                svc.Number = number++;
            }
        }

        private static void SetObjectNumbers(ref List<NetworkObject> objList)
        {
            long number = 1;
            foreach(var obj in objList)
            {
                obj.Number = number++;
            }
        }

        private static string ConstructOutput(ModellingObject inputObj, string type, long objNumber)
        {
            return ReportBase.ConstructLink(type, "", objNumber, inputObj.Display(), OutputLocation.export, $"a{inputObj.AppId}", "");
        }
    }
}
