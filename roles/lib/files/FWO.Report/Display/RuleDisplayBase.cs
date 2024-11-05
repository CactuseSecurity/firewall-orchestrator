using System.Text;
using FWO.Basics;
using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Report.Filter;

namespace FWO.Ui.Display
{
    public class RuleDisplayBase(UserConfig userConfig)
    {
        protected UserConfig userConfig = userConfig;

        public static string DisplayNumber(Rule rule)
        {
            return rule.DisplayOrderNumber.ToString();
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
            return result;
        }

        public static StringBuilder DisplayService(NetworkService service, ReportType reportType, string? serviceName = null)
        {
            return DisplayBase.DisplayService(service, reportType.IsTechReport(), serviceName);
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
    }
}
