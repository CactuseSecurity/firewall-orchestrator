﻿using FWO.Basics;
using FWO.Data.Modelling;

namespace FWO.Data.Report
{
    public class ConnectionReport
    {
        public string Name { get; set; } = "";

        public List<NetworkObject> AllObjects { get; set; } = [];
        public List<NetworkService> AllServices { get; set; } = [];

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

        public void PrepareObjectData(bool resolveNetworkAreas)
        {
            AllObjects = GetAllNetworkObjects(true, resolveNetworkAreas);
            SetObjectNumbers(AllObjects);
            AllServices = GetAllServices(true);
            SetSvcNumbers(AllServices);
        }

        public virtual List<NetworkObject> GetAllNetworkObjects(bool resolved = false, bool resolveNetworkAreas = false)
        {
            return [];
        }

        public virtual List<NetworkService> GetAllServices(bool resolved = false)
        {
            return [];
        }

        public static void SetSvcNumbers(List<NetworkService> svcList)
        {
            long number = 1;
            foreach(var svc in svcList)
            {
                svc.Number = number++;
            }
        }

        public static void SetObjectNumbers(List<NetworkObject> objList)
        {
            long number = 1;
            foreach(var obj in objList)
            {
                obj.Number = number++;
            }
        }

        public static List<NetworkService> GetAllServices(List<ModellingConnection> connections, bool resolved = false)
        {
            List<NetworkService> allServices = [];
            foreach(var conn in connections)
            {
                List<NetworkService> svcList = [];
                foreach (var svcGrp in conn.ServiceGroups.Select(s => s.Content))
                {
                    NetworkService serviceGroup = svcGrp.ToNetworkServiceGroup();
                    svcList.Add(svcGrp.ToNetworkServiceGroup());
                    if(resolved)
                    {
                        foreach(var svc in serviceGroup.ServiceGroups.Where(s => s.Object != null))
                        {
                            svcList.Add(svc.Object!);
                        }
                    }
                }
                allServices = allServices.Union(svcList).ToList();
                allServices = [.. allServices.Union([.. ModellingServiceWrapper.ResolveAsNetworkServices(conn.Services)])];
            }
            return allServices;
        }

        public static List<NetworkObject> GetAllNetworkObjects(List<ModellingConnection> connections, bool resolved = false, bool resolveNetworkAreas = false, long dummyARid = 0)
        {
            List<NetworkObject> allObjects = [];
            foreach(var conn in connections)
            {
                allObjects = [.. allObjects.Union(GetAllNwGrpObjectsFromConn(conn, resolved, resolveNetworkAreas, dummyARid))];
            }
            allObjects = [.. allObjects.Union(GetAllAppServers(connections).ConvertAll(ModellingAppServer.ToNetworkObject))];
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
                allAppServers = [.. allAppServers.Union([.. ModellingAppServerWrapper.Resolve(conn.SourceAppServers)])];
                allAppServers = [.. allAppServers.Union([.. ModellingAppServerWrapper.Resolve(conn.DestinationAppServers)])];
            }
            return allAppServers;
        }

        private static List<NetworkObject> GetAllNwGrpObjectsFromConn(ModellingConnection conn, bool resolved = false, bool resolveNetworkAreas = false, long dummyARid = 0)
        {
            List<NetworkObject> objList = [];
            GetObjectsFromAreas(conn.SourceAreas, ref objList, resolved, resolveNetworkAreas);
            GetObjectsFromAreas(conn.DestinationAreas, ref objList, resolved, resolveNetworkAreas);
            GetObjectsFromAR(conn.SourceAppRoles, ref objList, resolved, dummyARid);
            GetObjectsFromAR(conn.DestinationAppRoles, ref objList, resolved, dummyARid);
            GetObjectsFromOtherGroups(conn.SourceOtherGroups, ref objList, resolved);
            GetObjectsFromOtherGroups(conn.DestinationOtherGroups, ref objList, resolved);
            return objList;
        }

        private static void GetObjectsFromAreas(List<ModellingNetworkAreaWrapper> areas, ref List<NetworkObject> objectList, bool resolved = false, bool resolveNetworkAreas = false)
        {
            foreach (var areaWrapper in areas.Select(a => a.Content))
            {
                objectList.Add(areaWrapper.ToNetworkObjectGroup(false, resolveNetworkAreas));
                if(resolved && resolveNetworkAreas)
                {
                    foreach(var obj in areaWrapper.ToNetworkObjectGroup().ObjectGroups.Where(o => o.Object != null))
                    {
                        objectList.Add(obj.Object!);
                    }
                }
            }
        }

        private static void GetObjectsFromOtherGroups(List<ModellingNwGroupWrapper> nwGroups, ref List<NetworkObject> objectList, bool resolved = false)
        {
            foreach (var nwGrpWrapper in nwGroups.Select(n => n.Content))
            {
                objectList.Add(nwGrpWrapper.ToNetworkObjectGroup());
                if(resolved)
                {
                    foreach(var obj in nwGrpWrapper.ToNetworkObjectGroup().ObjectGroups.Where(o => o.Object != null))
                    {
                        objectList.Add(obj.Object!);
                    }
                }
            }
        }

        private static void GetObjectsFromAR(List<ModellingAppRoleWrapper> appRoles, ref List<NetworkObject> objectList, bool resolved = false, long dummyARid = 0)
        {
            foreach (var aRWrapper in appRoles.Select(w => w.Content).Where(a => a.Id != dummyARid))
            {
                objectList.Add(aRWrapper.ToNetworkObjectGroup());
                if(resolved)
                {
                    foreach(var obj in aRWrapper.ToNetworkObjectGroup().ObjectGroups.Where(o => o.Object != null))
                    {
                        objectList.Add(obj.Object!);
                    }
                 }
            }
        }

        public static string ListAppServers(List<ModellingAppServer> appServers, List<ModellingAppServer> surplusAppServers, bool diffMode = false, bool forExport = false)
        {
            if(diffMode)
            {
                List<string> allAppServers = [.. appServers.ConvertAll(a => DisplayAppServerWithDiff(a, forExport))];
                allAppServers.AddRange(surplusAppServers.ConvertAll(a => DisplayAppServerWithDiff(a, forExport, true)));
                return string.Join(", ", allAppServers);
            }
            else
            {
                return string.Join(", ", appServers.ConvertAll(a => DisplayBase.DisplayIpWithName(ModellingAppServer.ToNetworkObject(a))));
            }
        }

        private static string DisplayAppServerWithDiff(ModellingAppServer appServer, bool forExport, bool surplus = false)
        {
            string styleOrClass = $"{(forExport ? "style" : "class")}=\"{StyleOrCssClass(appServer, forExport, surplus)}\"";
            return $"<span {styleOrClass}>{DisplayBase.DisplayIpWithName(ModellingAppServer.ToNetworkObject(appServer))}</span>";
        }

        private static string StyleOrCssClass(ModellingAppServer appServer, bool forExport, bool surplus)
        {
            if (surplus)
            {
                return forExport ? GlobalConst.kStyleHighlightedGreen : "text-success";
            }
            if (appServer.NotImplemented)
            {
                return forExport ? GlobalConst.kStyleHighlightedRed : "text-danger";
            }
            return "";
        }
    }
}
