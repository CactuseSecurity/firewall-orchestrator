using FWO.Api.Data;
using FWO.Config.Api;
using System.Text;

namespace FWO.Ui.Display
{
    public class RuleDisplayBase
    {
        protected StringBuilder? result;
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

        public string DisplayIpRange(string Ip, string IpEnd)
        {
            return (Ip != null && Ip != "" ? $"{Ip}{(IpEnd != null && IpEnd != "" && IpEnd != Ip ? $"-{IpEnd}" : "")}" : "");
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

        public StringBuilder RemoveLastChars(StringBuilder s, int count)
        {
            string x = s.ToString(); 
            x = x.Remove(x.ToString().Length - count, count).ToString();
            return s.Remove(s.ToString().Length - count, count);
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

        public List<NetworkService> getNetworkServices(ServiceWrapper[] serviceArray)
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
    }
}
