using FWO.Api.Data;
using FWO.Config.Api;
using System.Text;
using FWO.Report.Filter;

namespace FWO.Ui.Display
{
    public class RuleDisplayHtml: RuleDisplayBase
    {

        public RuleDisplayHtml(UserConfig userConfig) : base(userConfig)
        {}

        public string DisplaySourceOrDestination(Rule rule, string style = "", string location = "report", ReportType reportType = ReportType.Rules, string side = "source")
        {
            if (location=="certification")
                reportType=ReportType.Rules;
            result = new StringBuilder();
            result.AppendLine("<p>");
            if (side=="source")
            {
                if (rule.SourceNegated)
                    result.AppendLine(userConfig.GetText("anything_but") + " <br>");
            }
            else if (side=="destination")
            {
                if (rule.DestinationNegated)
                    result.AppendLine(userConfig.GetText("anything_but") + " <br>");
            }

            switch (reportType)
            {
                case ReportType.Rules:
                case ReportType.NatRules:
                case ReportType.Recertification:
                    if (side == "source")
                    {
                        foreach (NetworkLocation networkLocation in rule.Froms)
                            result.Append(NetworkLocationToHtml(networkLocation, rule.MgmtId, location, style));
                    }
                    else if (side == "destination")
                    {
                        foreach (NetworkLocation networkLocation in rule.Tos)
                            result.Append(NetworkLocationToHtml(networkLocation, rule.MgmtId, location, style));
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

                    foreach (NetworkLocation networkLocation in userNwObjectList)
                        result.Append(NetworkLocationToHtml(networkLocation, rule.MgmtId, location, style, reportType=reportType));
                    break;
            }
            result.AppendLine("</p>");
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

        private StringBuilder NetworkLocationToHtml(NetworkLocation userNetworkObject, int mgmtId, string location = "", string style = "", ReportType reportType = ReportType.Rules)
        {
            string nwobjLink = "";
            string symbol = "oi oi-wrench";
            StringBuilder result = new StringBuilder();
            if (userNetworkObject.Object.Type.Name == "group")
                symbol = "oi oi-list-rich";
            else if (userNetworkObject.Object.Type.Name == "network")
                symbol = "oi oi-rss";
            else if (userNetworkObject.Object.Type.Name == "ip_range")
                symbol = "oi oi-resize-width";
            else
                symbol = "oi oi-monitor";
            
            if (userNetworkObject.User?.Id != null)
            {
                if (reportType==ReportType.ResolvedRulesTech)
                    result.Append($"{userNetworkObject.User.Name}@");
                else
                {
                    string userLink = location == "" ? $"user{userNetworkObject.User.Id}" : $"goto-report-m{mgmtId}-user{userNetworkObject.User.Id}";
                    result.Append($"<span class=\"oi oi-people\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"{location}#{userLink}\" target=\"_top\" style=\"{style}\">{userNetworkObject.User.Name}</a>@");
                }
            }

            nwobjLink = location == "" ? $"nwobj{userNetworkObject.Object.Id}" : $"goto-report-m{mgmtId}-nwobj{userNetworkObject.Object.Id}";

            if (reportType==ReportType.Rules || reportType==ReportType.ResolvedRules || reportType==ReportType.NatRules)
            {
                result.Append($"<span class=\"{symbol}\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"{location}#{nwobjLink}\" target=\"_top\" style=\"{style}\">{userNetworkObject.Object.Name}</a>");
                if (userNetworkObject.Object.Type.Name != "group")
                    result.Append(" (");
            }
            result.Append(DisplayIpRange(userNetworkObject.Object.IP, userNetworkObject.Object.IpEnd));
            if (userNetworkObject.Object.Type.Name != "group" && (reportType==ReportType.Rules || reportType==ReportType.ResolvedRules || reportType==ReportType.NatRules))
                result.Append(")");
            result.AppendLine("<br>");
            return result;
        }

        public string DisplayService(Rule rule, string style = "", string location = "report", ReportType reportType = ReportType.Rules)
        {
            if (location=="certification")
                reportType=ReportType.Rules;
            result = new StringBuilder();
            result.AppendLine("<p>");
            if (rule.ServiceNegated)
                result.AppendLine(userConfig.GetText("anything_but") + " <br>");

            switch (reportType)
            {
                case ReportType.Rules:
                case ReportType.NatRules:
                case ReportType.Recertification:
                    foreach (ServiceWrapper service in rule.Services)
                        result.Append(ServiceToHtml(service.Content, rule.MgmtId, location, style, reportType=reportType));
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

                    foreach (NetworkService service in serviceList)
                        result.Append(ServiceToHtml(service, rule.MgmtId, location, style, reportType=reportType));
                    break;
            }
            result.AppendLine("</p>");
            return result.ToString();
        }
        private StringBuilder ServiceToHtml(NetworkService service, int mgmtId, string location = "", string style = "", ReportType reportType = ReportType.Rules)
        {
            string link = "";
            string symbol = "oi oi-wrench";
            StringBuilder result = new StringBuilder();
            if (service.Type.Name == "group")
                symbol = "oi oi-list-rich";
            else
                symbol = "oi oi-wrench";
            link = location == "" ? $"svc{service.Id}" : $"goto-report-m{mgmtId}-svc{service.Id}";
            if (reportType==ReportType.Rules || reportType==ReportType.ResolvedRules || reportType==ReportType.NatRules)
                result.Append($"<span class=\"{symbol}\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"{location}#{link}\" target=\"_top\" style=\"{style}\">{service.Name}</a>");

            if (service.DestinationPort != null)
            {
                if (reportType==ReportType.Rules || reportType==ReportType.ResolvedRules || reportType==ReportType.NatRules)
                    result.Append(" (");
                result.Append(service.DestinationPort == service.DestinationPortEnd ? $"{service.DestinationPort}/{service.Protocol?.Name}"
                    : $" {service.DestinationPort}-{service.DestinationPortEnd}/{service.Protocol?.Name}");
                if (reportType==ReportType.Rules || reportType==ReportType.ResolvedRules)
                    result.Append(")");
            }
            else if (reportType==ReportType.ResolvedRulesTech)
            {
                // if no port can be displayed, use the service name as fall-back
                result.Append($"{service.Name}");
            }
            result.AppendLine("<br>");
            return result;
        }

        public string DisplayEnabled(Rule rule, bool export = false)
        {
            if (export)
            {
                return $"<b>{(rule.Disabled ? "N" : "Y")}</b>";
            }
            else
            {
                return $"<div class=\"oi {(rule.Disabled ? "oi-x" : "oi-check")}\"></div>";
            }
        }
    }
}
