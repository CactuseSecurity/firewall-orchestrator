using FWO.Api.Data;
using FWO.Config.Api;
using System.Text;
using FWO.Report.Filter;

namespace FWO.Ui.Display
{
    public class RuleDisplayJson : RuleDisplayBase
    {

        public RuleDisplayJson(UserConfig userConfig) : base(userConfig)
        { }
        public new string DisplayNumber(Rule rule, Rule[] rules)
        {
            return $"\"number\": {rule.DisplayOrderNumber.ToString()},";
        }
        public new string DisplayName(Rule rule)
        {
            return (rule.Name != null ? $"\"name\": \"{rule.Name}\"," : "");
        }
        public new string DisplaySourceZone(Rule rule)
        {
            return (rule.SourceZone != null ? $"\"source zone\": \"{rule.SourceZone.Name}\"," : "");
        }

        public new string DisplayDestinationZone(Rule rule)
        {
            return (rule.DestinationZone != null ? $"\"destination zone\": \"{rule.DestinationZone.Name}\"," : "");
        }

        public new string DisplayAction(Rule rule)
        {
            return $"\"action\": \"{rule.Action}\",";
        }

        public new string DisplayTrack(Rule rule)
        {
            return $"\"tracking\": \"{rule.Track}\",";
        }

        public new string DisplayUid(Rule rule)
        {
            return (rule.Uid != null ? $"\"rule uid\": \"{rule.Uid}\"," : "");
        }

        public new string DisplayComment(Rule rule)
        {
            return (rule.Comment != null ? $"\"comment\": \"{rule.Comment}\"," : "");
        }

        public string DisplaySourceOrDestination(Rule rule, string style = "", string location = "report", ReportType reportType = ReportType.Rules, string side = "source")
        {
            result = new StringBuilder();
            if (side=="source")
            {
                if (rule.SourceNegated)
                    result.AppendLine($"\"{side} negated\": {rule.SourceNegated.ToString().ToLower()},");
            }
            else if (side=="destination")
            {
                if (rule.DestinationNegated)
                    result.AppendLine($"\"{side} negated\": {rule.DestinationNegated.ToString().ToLower()},");
            }

            result.Append($"\"{side}\": [");

            switch (reportType)
            {
                case ReportType.Rules:
                    if (side == "source")
                    {
                        foreach (NetworkLocation networkLocation in rule.Froms)
                            result.Append(NetworkLocationToJson(networkLocation, rule.MgmtId, location, style));
                    }
                    else if (side == "destination")
                    {
                        foreach (NetworkLocation networkLocation in rule.Tos)
                            result.Append(NetworkLocationToJson(networkLocation, rule.MgmtId, location, style));
                    }
                    break;
                case ReportType.ResolvedRules:
                case ReportType.ResolvedRulesTech:
                    HashSet<NetworkObject> collectedNetworkObjects = new HashSet<NetworkObject>();
                    HashSet<NetworkLocation> collectedUserNetworkObjects = new HashSet<NetworkLocation>();
                    if (side == "source")
                    {
                        foreach (NetworkLocation networkObject in rule.Froms)
                        {
                            foreach (GroupFlat<NetworkObject> nwObject in networkObject.Object.ObjectGroupFlats)
                                if (nwObject.Object != null && nwObject.Object.Type.Name != "group")    // leave out group level altogether
                                    collectedUserNetworkObjects.Add(new NetworkLocation(networkObject.User, nwObject.Object));
                        }
                    }
                    else if (side == "destination")
                    {
                        foreach (NetworkLocation networkObject in rule.Tos)
                        {
                            foreach (GroupFlat<NetworkObject> nwObject in networkObject.Object.ObjectGroupFlats)
                                if (nwObject.Object != null && nwObject.Object.Type.Name != "group")    // leave out group level altogether
                                    collectedUserNetworkObjects.Add(new NetworkLocation(networkObject.User, nwObject.Object));
                        }
                    }

                    List<NetworkLocation> userNwObjectList = collectedUserNetworkObjects.ToList<NetworkLocation>();
                    userNwObjectList.Sort();

                    StringBuilder cell = new StringBuilder();
                    foreach (NetworkLocation networkLocation in userNwObjectList)
                    {
                        cell.Append(NetworkLocationToJson(networkLocation, rule.MgmtId, location, style, reportType=reportType).ToString());
                    }
                    cell.Remove(cell.ToString().Length - 1, 1);  // get rid of final comma
                    result.Append($"{cell}],");
                    break;
            }
            return result.ToString();
        }

        public string DisplaySource(Rule rule, string style = "", string location = "report", ReportType reportType = ReportType.Rules)
        {
            return DisplaySourceOrDestination(rule, style, location, reportType, side: "source");
        }

        public string DisplayDestination(Rule rule, string style = "", string location = "report", ReportType reportType = ReportType.Rules)
        {
            return DisplaySourceOrDestination(rule, style, location, reportType, side: "destination");
        }

        private StringBuilder NetworkLocationToJson(NetworkLocation userNetworkObject, int mgmtId, string location = "", string style = "", ReportType reportType = ReportType.Rules)
        {
            StringBuilder result = new StringBuilder();

            result.Append("\"");
            if (userNetworkObject.User?.Id != null)
            {
                result.Append($"{userNetworkObject.User.Name}@");
            }

            if (reportType!=ReportType.ResolvedRulesTech)
            {
                result.Append($"{userNetworkObject.Object.Name}");
                result.Append(" (");
            }
            result.Append(DisplayIpRange(userNetworkObject.Object.IP, userNetworkObject.Object.IpEnd));
            if (reportType!=ReportType.ResolvedRulesTech)
            {
                result.Append(")");
            }
            result.Append("\",");
            return result;
        }

        public string DisplayService(Rule rule, string style = "", string location = "report", ReportType reportType = ReportType.Rules)
        {
            result = new StringBuilder();
            if (rule.ServiceNegated)
                result.AppendLine($"\"service negated\": {rule.ServiceNegated.ToString().ToLower()},");

            result.Append($"\"service\": [");

            switch (reportType)
            {
                case ReportType.Rules:
                    foreach (ServiceWrapper service in rule.Services)
                        result.Append(ServiceToJson(service.Content, rule.MgmtId, location, style));
                    break;
                case ReportType.ResolvedRules:
                case ReportType.ResolvedRulesTech:
                    HashSet<NetworkService> collectedServices = new HashSet<NetworkService>();
                    foreach (ServiceWrapper service in rule.Services)
                        foreach (GroupFlat<NetworkService> nwService in service.Content.ServiceGroupFlats)
                            if (nwService.Object != null && nwService.Object.Type.Name != "group")
                                collectedServices.Add(nwService.Object);

                    List<NetworkService> serviceList = collectedServices.ToList<NetworkService>();
                    serviceList.Sort(delegate (NetworkService x, NetworkService y) { return x.Name.CompareTo(y.Name); });

                    StringBuilder cell = new StringBuilder();
                    foreach (NetworkService service in serviceList)
                        cell.Append(ServiceToJson(service, rule.MgmtId, location, style, reportType=reportType).ToString());
                    
                    cell.Remove(cell.ToString().Length - 1, 1);  // get rid of final comma
                    result.Append($"{cell}],");
                    break;
            }
            return result.ToString();
        }
        private StringBuilder ServiceToJson(NetworkService service, int mgmtId, string location = "", string style = "", ReportType reportType = ReportType.Rules)
        {
            StringBuilder result = new StringBuilder();
            result.Append("\"");
            if (reportType != ReportType.ResolvedRulesTech)
            {
                result.Append($"{service.Name}");
                if (service.DestinationPort != null)
                    result.Append(service.DestinationPort == service.DestinationPortEnd ? $" ({service.DestinationPort}/{service.Protocol?.Name})"
                        : $" ({service.DestinationPort}-{service.DestinationPortEnd}/{service.Protocol?.Name})");
            }
            else 
            {
                if (service.DestinationPort == null)
                    result.Append($"{service.Name}");
                else
                    result.Append(service.DestinationPort == service.DestinationPortEnd ? $"{service.DestinationPort}/{service.Protocol?.Name}"
                        : $"{service.DestinationPort}-{service.DestinationPortEnd}/{service.Protocol?.Name}");
            }
            result.Append("\",");
            return result;
        }

        public string DisplayEnabled(Rule rule, bool export = false)
        {
            return $"\"disabled\": {rule.Disabled.ToString().ToLower()},";
        }
    }
}
