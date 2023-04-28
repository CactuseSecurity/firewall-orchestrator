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

        public new string DisplayNumber(Rule rule)
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

        private string DisplaySourceOrDestination(Rule rule, ReportType reportType = ReportType.Rules, bool isSource = true)
        {
            result = new StringBuilder();
            if (isSource)
            {
                result.AppendLine($"\"source negated\": {rule.SourceNegated.ToString().ToLower()},");
            }
            else
            {
                result.AppendLine($"\"destination negated\": {rule.DestinationNegated.ToString().ToLower()},");
            }

            result.Append($"{(isSource ? "\"source\"" : "\"destination\"")}: [");

            if (reportType.IsResolvedReport())
            {
                List<NetworkLocation> userNwObjectList = getNetworkLocations(isSource ? rule.Froms : rule.Tos);
                StringBuilder cell = new StringBuilder();
                foreach (NetworkLocation networkLocation in userNwObjectList)
                {
                    cell.Append(NetworkLocationToJson(networkLocation, reportType).ToString());
                }
                if(cell.ToString().Length > 0)
                {
                    cell.Remove(cell.ToString().Length - 1, 1);  // get rid of final comma
                }
                result.Append($"{cell}],");
            }
            else
            {
                foreach (NetworkLocation networkLocation in isSource ? rule.Froms : rule.Tos)
                {
                    result.Append(NetworkLocationToJson(networkLocation));
                }
            }
            return result.ToString();
        }

        public string DisplaySource(Rule rule, ReportType reportType = ReportType.Rules)
        {
            return DisplaySourceOrDestination(rule, reportType, true);
        }

        public string DisplayDestination(Rule rule, ReportType reportType = ReportType.Rules)
        {
            return DisplaySourceOrDestination(rule, reportType, false);
        }

        private StringBuilder NetworkLocationToJson(NetworkLocation userNetworkObject, ReportType reportType = ReportType.Rules)
        {
            StringBuilder result = new StringBuilder();

            result.Append("\"");
            if (userNetworkObject.User?.Id != null)
            {
                result.Append($"{userNetworkObject.User.Name}@");
            }

            if (!reportType.IsTechReport())
            {
                result.Append($"{userNetworkObject.Object.Name}");
                result.Append(" (");
            }
            result.Append(DisplayIpRange(userNetworkObject.Object.IP, userNetworkObject.Object.IpEnd));
            if (!reportType.IsTechReport())
            {
                result.Append(")");
            }
            result.Append("\",");
            return result;
        }

        public string DisplayService(Rule rule, ReportType reportType = ReportType.Rules)
        {
            result = new StringBuilder();
            result.AppendLine($"\"service negated\": {rule.ServiceNegated.ToString().ToLower()},");
            result.Append($"\"service\": [");

            if (reportType.IsResolvedReport())
            {
                List<NetworkService> serviceList = getNetworkServices(rule.Services);
                StringBuilder cell = new StringBuilder();
                foreach (NetworkService service in serviceList)
                {
                    cell.Append(ServiceToJson(service, reportType).ToString());
                }
                if(cell.ToString().Length > 0)
                {
                    cell.Remove(cell.ToString().Length - 1, 1);  // get rid of final comma
                }
                result.Append($"{cell}],");
            }
            else
            {
                foreach (ServiceWrapper service in rule.Services)
                {
                    result.Append(ServiceToJson(service.Content));
                }
            }
            return result.ToString();
        }

        private StringBuilder ServiceToJson(NetworkService service, ReportType reportType = ReportType.Rules)
        {
            StringBuilder result = new StringBuilder();
            result.Append("\"");
            if (reportType.IsTechReport())
            {
                if (service.DestinationPort == null)
                    result.Append($"{service.Name}");
                else
                    result.Append(service.DestinationPort == service.DestinationPortEnd ? $"{service.DestinationPort}/{service.Protocol?.Name}"
                        : $"{service.DestinationPort}-{service.DestinationPortEnd}/{service.Protocol?.Name}");
            }
            else
            {
                result.Append($"{service.Name}");
                if (service.DestinationPort != null)
                    result.Append(service.DestinationPort == service.DestinationPortEnd ? $" ({service.DestinationPort}/{service.Protocol?.Name})"
                        : $" ({service.DestinationPort}-{service.DestinationPortEnd}/{service.Protocol?.Name})");
            }
            result.Append("\",");
            return result;
        }

        public string DisplayEnabled(Rule rule)
        {
            return $"\"disabled\": {rule.Disabled.ToString().ToLower()},";
        }
    }
}
