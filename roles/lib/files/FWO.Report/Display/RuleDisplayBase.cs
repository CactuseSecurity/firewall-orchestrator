using NetTools;
using System.Net;
using System.Numerics;
using System.Text;
using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Report.Filter;
using FWO.Logging;

namespace FWO.Ui.Display
{
    public class RuleDisplayBase
    {
        protected UserConfig userConfig;

        public RuleDisplayBase(UserConfig userConfig)
        {
            this.userConfig = userConfig;
        }

        public string DisplayNumber(Rule rule)
        {
            return rule.DisplayOrderNumber.ToString();
        }

        public string DisplayName(Rule rule)
        {
            return (rule.Name != null ? rule.Name : "");
        }

        public string DisplaySourceZone(Rule rule)
        {
            return (rule.SourceZone != null ? rule.SourceZone.Name : "");
        }

        public string DisplayDestinationZone(Rule rule)
        {
            return (rule.DestinationZone != null ? rule.DestinationZone.Name : "");
        }

        public string DisplayAction(Rule rule)
        {
            return rule.Action;
        }

        public string DisplayTrack(Rule rule)
        {
            return rule.Track;
        }

        public string DisplayUid(Rule rule)
        {
            return (rule.Uid != null ? rule.Uid : "");
        }

        public string DisplayComment(Rule rule)
        {
            return (rule.Comment != null ? rule.Comment : "");
        }

        public StringBuilder DisplayNetworkLocation(NetworkLocation userNetworkObject, ReportType reportType, string? userName = null, string? objName = null)
        {
            StringBuilder result = new StringBuilder();
            
            if (userNetworkObject.User != null &&  userNetworkObject.User.Id > 0)
            {
                result.Append($"{userName ?? userNetworkObject.User.Name}@");
            }

            if (!reportType.IsTechReport())
            {
                result.Append($"{objName ?? userNetworkObject.Object.Name}");
            }
            if (userNetworkObject.Object.Type.Name != "group")
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

        public StringBuilder DisplayService(NetworkService service, ReportType reportType, string? serviceName = null)
        {
            StringBuilder result = new StringBuilder();
            if (reportType.IsTechReport())
            {
                if (service.DestinationPort == null)
                {
                    result.Append($"{service.Name}");
                }
                else
                {
                    result.Append(service.DestinationPort == service.DestinationPortEnd ? $"{service.DestinationPort}/{service.Protocol?.Name}"
                        : $"{service.DestinationPort}-{service.DestinationPortEnd}/{service.Protocol?.Name}");
                }
            }
            else
            {
                result.Append($"{serviceName ?? service.Name}");
                if (service.DestinationPort != null)
                {
                    result.Append(service.DestinationPort == service.DestinationPortEnd ? $" ({service.DestinationPort}/{service.Protocol?.Name})"
                        : $" ({service.DestinationPort}-{service.DestinationPortEnd}/{service.Protocol?.Name})");
                }
            }
            return result;
        }

        public StringBuilder RemoveLastChars(StringBuilder s, int count)
        {
            string x = s.ToString(); 
            x = x.Remove(x.ToString().Length - count, count).ToString();
            return s.Remove(s.ToString().Length - count, count);
        }

        public string Quote(string? input)
        {
            return  $"\"{input ?? ""}\"";
        }

        public List<NetworkLocation> getNetworkLocations(NetworkLocation[] locationArray)
        {
            HashSet<NetworkLocation> collectedUserNetworkObjects = new HashSet<NetworkLocation>();
            foreach (NetworkLocation networkObject in locationArray)
            {
                foreach (GroupFlat<NetworkObject> nwObject in networkObject.Object.ObjectGroupFlats)
                {
                    if (nwObject.Object != null && nwObject.Object.Type.Name != "group")    // leave out group level altogether
                    {
                        collectedUserNetworkObjects.Add(new NetworkLocation(networkObject.User, nwObject.Object));
                    }
                }
            }
            List<NetworkLocation> userNwObjectList = collectedUserNetworkObjects.ToList<NetworkLocation>();
            userNwObjectList.Sort();
            return userNwObjectList;
        }

        public List<NetworkService> GetNetworkServices(ServiceWrapper[] serviceArray)
        {
            HashSet<NetworkService> collectedServices = new HashSet<NetworkService>();
            foreach (ServiceWrapper service in serviceArray)
            {
                foreach (GroupFlat<NetworkService> nwService in service.Content.ServiceGroupFlats)
                {
                    if (nwService.Object != null && nwService.Object.Type.Name != "group")
                    {
                        collectedServices.Add(nwService.Object);
                    }
                }
            }
            List<NetworkService> serviceList = collectedServices.ToList<NetworkService>();
            serviceList.Sort(delegate (NetworkService x, NetworkService y) { return x.Name.CompareTo(y.Name); });
            return serviceList;
        }

        protected void AnalyzeElements(string oldElement, string newElement, ref List<string> unchanged, ref List<string> deleted, ref List<string> added)
        {
            string[] separatingStrings = { "," };
            string[] oldAr = oldElement.Split(separatingStrings, System.StringSplitOptions.RemoveEmptyEntries);
            string[] newAr = newElement.Split(separatingStrings, System.StringSplitOptions.RemoveEmptyEntries);

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
