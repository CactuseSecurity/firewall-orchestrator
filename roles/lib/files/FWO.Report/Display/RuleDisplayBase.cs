﻿using System.Text;
using FWO.Basics;
using FWO.Data;
using FWO.Config.Api;
using FWO.Report;
using FWO.Report.Filter;
using FWO.Report;

namespace FWO.Ui.Display
{
    public class RuleDisplayBase(UserConfig userConfig)
    {
        protected UserConfig userConfig = userConfig;

        public static string DisplayNumber(Rule rule)
        {
            return rule.DisplayOrderNumberString;
        }

        public static string DisplayEnabled(Rule rule, OutputLocation location)
        {
            if (location == OutputLocation.export)
            {
                return $"<b>{(rule.Disabled ? "N" : "Y")}</b>";
            }
            else
            {
                return $"<div class=\"oi {(rule.Disabled ? "oi-x" : "oi-check")}\"></div>";
            }
        }
        public static string DisplayName(Rule rule)
        {
            return rule.Name ?? "";
        }

        public static string DisplaySourceZone(Rule rule)
        {
            return rule.SourceZone != null ? rule.SourceZone.Name : "";
        }

        public static string DisplayDestinationZone(Rule rule)
        {
            return rule.DestinationZone != null ? rule.DestinationZone.Name : "";
        }

        public static string DisplayAction(Rule rule)
        {
            return rule.Action;
        }

        public static string DisplayTrack(Rule rule)
        {
            return rule.Track;
        }

        public static string DisplayUid(Rule rule)
        {
            return rule.Uid ?? "";
        }

        public static string DisplayComment(Rule rule)
        {
            return rule.Comment ?? "";
        }

        public static StringBuilder DisplayNetworkLocation(NetworkLocation userNetworkObject, ReportType reportType, string? userName = null, string? objName = null)
        {
            StringBuilder result = new();
            
            if (userNetworkObject.User != null &&  userNetworkObject.User.Id > 0)
            {
                result.Append($"{userName ?? userNetworkObject.User.Name}@");
            }

            if (!reportType.IsTechReport())
            {
                result.Append($"{objName ?? userNetworkObject.Object.Name}");
            }
            if (userNetworkObject.Object.Type.Name != ObjectType.Group)
            {
                bool showIpinBrackets = !reportType.IsTechReport();
                result.Append(NwObjDisplay.DisplayIp(
                    userNetworkObject.Object.IP,
                    userNetworkObject.Object.IpEnd,
                    userNetworkObject.Object.Type.Name,
                    showIpinBrackets));
            }
            return reportType == ReportType.VarianceAnalysis ? DisplayWithIcon(result, ObjCategory.nobj, userNetworkObject.Object.Type.Name) : result;
        }

        public static StringBuilder DisplayService(NetworkService service, ReportType reportType, string? serviceName = null)
        {
            StringBuilder result = DisplayBase.DisplayService(service, reportType.IsTechReport(), serviceName);
            return reportType == ReportType.VarianceAnalysis ? DisplayWithIcon(result, ObjCategory.nsrv, service.Type.Name) : result;
        }
        public static StringBuilder DisplayGateway(Device gateway, ReportType reportType, string? gatewayName = null)
        {
            return DisplayBase.DisplayGateway(gateway, reportType.IsTechReport(), gatewayName);
        }

        public static StringBuilder RemoveLastChars(StringBuilder s, int count)
        {
            string x = s.ToString(); 
            x = x.Remove(x.ToString().Length - count, count).ToString();
            return s.Remove(s.ToString().Length - count, count);
        }

        public static string Quote(string? input)
        {
            return  $"\"{input ?? ""}\"";
        }

        public static List<NetworkLocation> GetNetworkLocations(NetworkLocation[] locationArray)
        {
            HashSet<NetworkLocation> collectedUserNetworkObjects = [];
            foreach (NetworkLocation networkObject in locationArray)
            {
                foreach (GroupFlat<NetworkObject> nwObject in networkObject.Object.ObjectGroupFlats)
                {
                    if (nwObject.Object != null && nwObject.Object.Type.Name != ObjectType.Group)    // leave out group level altogether
                    {
                        collectedUserNetworkObjects.Add(new NetworkLocation(networkObject.User, nwObject.Object));
                    }
                }
            }
            List<NetworkLocation> userNwObjectList = [.. collectedUserNetworkObjects];
            userNwObjectList.Sort();
            return userNwObjectList;
        }

        public static List<NetworkService> GetNetworkServices(ServiceWrapper[] serviceArray)
        {
            HashSet<NetworkService> collectedServices = [];
            foreach (ServiceWrapper service in serviceArray)
            {
                foreach (GroupFlat<NetworkService> nwService in service.Content.ServiceGroupFlats)
                {
                    if (nwService.Object != null && nwService.Object.Type.Name != ObjectType.Group)
                    {
                        collectedServices.Add(nwService.Object);
                    }
                }
            }
            List<NetworkService> serviceList = [.. collectedServices];
            serviceList.Sort(delegate (NetworkService x, NetworkService y) { return x.Name.CompareTo(y.Name); });
            return serviceList;
        }

        protected static void AnalyzeElements(string oldElement, string newElement, ref List<string> unchanged, ref List<string> deleted, ref List<string> added)
        {
            string[] separatingStrings = [","];
            string[] oldAr = oldElement.Split(separatingStrings, StringSplitOptions.RemoveEmptyEntries);
            string[] newAr = newElement.Split(separatingStrings, StringSplitOptions.RemoveEmptyEntries);

            foreach (var item in oldAr)
            {
                if (newAr.Contains(item))
                {
                    unchanged.Add(item);
                }
                else
                {
                    string deletedItem = item;
                    deleted.Add(deletedItem);
                }
            }
            foreach (var item in newAr)
            {
                if (!oldAr.Contains(item))
                {
                    string newItem = item; 
                    added.Add(newItem);
                }
            }
        }

        private static StringBuilder DisplayWithIcon(StringBuilder outputString, ObjCategory? objCategory, string? objType)
        {
            string symbol = ReportBase.GetIconClass(objCategory, objType);
            return new($"<span class=\"{symbol}\">{outputString}</span>");
        }
    }
}
