using FWO.Api.Data;
using FWO.Config.Api;
using System.Text;
using FWO.Report.Filter;

namespace FWO.Ui.Display
{
    public class RuleDisplay
    {
        protected StringBuilder? result;
        protected UserConfig userConfig;

        public RuleDisplay(UserConfig userConfig)
        {
            this.userConfig = userConfig;
        }

        public string DisplayNumber(Rule rule, Rule[] rules)
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

        public string DisplaySourceOrDestination(Rule rule, string style = "", string location = "report", ReportType reportType = ReportType.Rules, string side = "source")
        {
            // TODO: when switching from Rules to ResolvedRules reportType: clear all data as all src/dst are shown as empty otherwise

            result = new StringBuilder();
            result.AppendLine("<p>");
            if (rule.SourceNegated)
                result.AppendLine(userConfig.GetText("anything_but") + " <br>");

            switch (reportType)
            {
                case ReportType.Rules:
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
                    userNwObjectList.Sort(delegate (NetworkLocation x, NetworkLocation y) { return x.CompareTo(y); });

                    foreach (NetworkLocation networkLocation in userNwObjectList)
                        result.Append(NetworkLocationToHtml(networkLocation, rule.MgmtId, location, style));
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

        private StringBuilder NetworkLocationToHtml(NetworkLocation userNetworkObject, int mgmtId, string location = "", string style = "")
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
                string userLink = location == "" ? $"user{userNetworkObject.User.Id}" : $"goto-report-m{mgmtId}-user{userNetworkObject.User.Id}";
                result.AppendLine($"<span class=\"oi oi-people\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"{location}#{userLink}\" target=\"_top\" style=\"{style}\">{userNetworkObject.User.Name}</a>@");
            }

            nwobjLink = location == "" ? $"nwobj{userNetworkObject.Object.Id}" : $"goto-report-m{mgmtId}-nwobj{userNetworkObject.Object.Id}";

            result.Append($"<span class=\"{symbol}\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"{location}#{nwobjLink}\" target=\"_top\" style=\"{style}\">{userNetworkObject.Object.Name}</a>");
            result.Append(DisplayIpRange(userNetworkObject.Object.IP, userNetworkObject.Object.IpEnd));
            result.AppendLine("<br>");
            return result;
        }

        public string DisplayIpRange(string Ip, string IpEnd)
        {
            return (Ip != null && Ip != "" ? $" ({Ip}{(IpEnd != null && IpEnd != "" && IpEnd != Ip ? $"-{IpEnd}" : "")})" : "");
        }

        public string DisplayService(Rule rule, string style = "", string location = "report", ReportType reportType = ReportType.Rules)
        {
            result = new StringBuilder();
            result.AppendLine("<p>");
            if (rule.ServiceNegated)
                result.AppendLine(userConfig.GetText("anything_but") + " <br>");

            switch (reportType)
            {
                case ReportType.Rules:
                    foreach (ServiceWrapper service in rule.Services)
                        result.Append(ServiceToHtml(service.Content, rule.MgmtId, location, style));
                    break;
                case ReportType.ResolvedRules:
                    HashSet<NetworkService> collectedServices = new HashSet<NetworkService>();
                    foreach (ServiceWrapper service in rule.Services)
                        foreach (GroupFlat<NetworkService> nwService in service.Content.ServiceGroupFlats)
                            if (nwService.Object != null && nwService.Object.Type.Name != "group")
                                collectedServices.Add(nwService.Object);

                    List<NetworkService> serviceList = collectedServices.ToList<NetworkService>();
                    serviceList.Sort(delegate (NetworkService x, NetworkService y) { return x.Name.CompareTo(y.Name); });

                    foreach (NetworkService service in serviceList)
                        result.Append(ServiceToHtml(service, rule.MgmtId, location, style));
                    break;
            }
            result.AppendLine("</p>");
            return result.ToString();
        }
        private StringBuilder ServiceToHtml(NetworkService service, int mgmtId, string location = "", string style = "")
        {
            string link = "";
            string symbol = "oi oi-wrench";
            StringBuilder result = new StringBuilder();
            if (service.Type.Name == "group")
                symbol = "oi oi-list-rich";
            else
                symbol = "oi oi-wrench";
            link = location == "" ? $"svc{service.Id}" : $"goto-report-m{mgmtId}-svc{service.Id}";
            result.Append($"<span class=\"{symbol}\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"{location}#{link}\" target=\"_top\" style=\"{style}\">{service.Name}</a>");

            if (service.DestinationPort != null)
                result.Append(service.DestinationPort == service.DestinationPortEnd ? $" ({service.DestinationPort}/{service.Protocol?.Name})"
                    : $" ({service.DestinationPort}-{service.DestinationPortEnd}/{service.Protocol?.Name})");
            result.AppendLine("<br>");
            return result;
        }
        public string DisplayAction(Rule rule)
        {
            return rule.Action;
        }

        public string DisplayTrack(Rule rule)
        {
            return rule.Track;
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

        public string DisplayUid(Rule rule)
        {
            return (rule.Uid != null ? rule.Uid : "");
        }

        public string DisplayComment(Rule rule)
        {
            return (rule.Comment != null ? rule.Comment : "");
        }
    }
}
