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

        public string DisplaySource(Rule rule, string style = "", string location = "report", ReportType reportType = ReportType.Rules)
        {
            result = new StringBuilder();

            result.AppendLine("<p>");

            if (rule.SourceNegated)
                result.AppendLine(userConfig.GetText("anything_but") + " <br>");

            string symbol = "";
            string nwobjLink = "";
            foreach (NetworkLocation source in rule.Froms)
            {
                if (reportType == ReportType.Rules)
                {
                    if (source.Object.Type.Name == "group")
                        symbol = "oi oi-list-rich";
                    else if (source.Object.Type.Name == "network")
                        symbol = "oi oi-rss";
                    else if (source.Object.Type.Name == "ip_range")
                        symbol = "oi oi-resize-width";
                    else
                        symbol = "oi oi-monitor";

                    string userLink = location == "" ? $"user{source.User?.Id}"
                                                    : $"goto-report-m{rule.MgmtId}-user{source.User?.Id}";

                    nwobjLink = location == "" ? $"nwobj{source.Object.Id}"
                                                    : $"goto-report-m{rule.MgmtId}-nwobj{source.Object.Id}";

                    if (source.User != null)
                        result.AppendLine($"<span class=\"oi oi-people\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"{location}#{userLink}\" target=\"_top\" style=\"{style}\">{source.User.Name}</a>@");
                    result.Append($"<span class=\"{symbol}\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"{location}#{nwobjLink}\" target=\"_top\" style=\"{style}\">{source.Object.Name}</a>");
                    result.Append(DisplayIpRange(source.Object.IP, source.Object.IpEnd));
                    result.AppendLine("<br>");
                }
                else if (reportType == ReportType.ResolvedRules)
                {
                    if (source.Object.Type.Name == "group")
                        result.Append(resolveNetworkGroup(source.Object.ObjectGroupFlats, location, rule, style));
                    else if (source.Object.Type.Name == "network")
                        symbol = "oi oi-rss";
                    else if (source.Object.Type.Name == "ip_range")
                        symbol = "oi oi-resize-width";
                    else
                        symbol = "oi oi-monitor";

                    string userLink = location == "" ? $"user{source.User?.Id}"
                                                    : $"goto-report-m{rule.MgmtId}-user{source.User?.Id}";

                    nwobjLink = location == "" ? $"nwobj{source.Object.Id}"
                                                    : $"goto-report-m{rule.MgmtId}-nwobj{source.Object.Id}";

                    if (source.User != null)
                        result.AppendLine($"<span class=\"oi oi-people\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"{location}#{userLink}\" target=\"_top\" style=\"{style}\">{source.User.Name}</a>@");
                    result.Append($"<span class=\"{symbol}\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"{location}#{nwobjLink}\" target=\"_top\" style=\"{style}\">{source.Object.Name}</a>");
                    result.Append(DisplayIpRange(source.Object.IP, source.Object.IpEnd));
                    result.AppendLine("<br>");
                }
            }
            result.AppendLine("</p>");
            return result.ToString();
        }

        private StringBuilder resolveNetworkGroup(GroupFlat<NetworkObject>[] group, string location, Rule rule, string style)
        {
            string symbol = "";
            string nwobjLink = "";
            StringBuilder result = new StringBuilder();
            foreach (GroupFlat<NetworkObject> nwObject in group)
            {
                if (nwObject.Object.Type.Name != "group")
                {
                    if (nwObject.Object.Type.Name == "network")
                        symbol = "oi oi-rss";
                    else if (nwObject.Object.Type.Name == "ip_range")
                        symbol = "oi oi-resize-width";
                    else
                        symbol = "oi oi-monitor";
                    nwobjLink = location == "" ? $"nwobj{nwObject.Object.Id}" : $"goto-report-m{rule.MgmtId}-nwobj{nwObject.Object.Id}";
                    result.Append($"<span class=\"{symbol}\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"{location}#{nwobjLink}\" target=\"_top\" style=\"{style}\">{nwObject.Object.Name}</a><br>");
                    result.Append(DisplayIpRange(nwObject.Object.IP, nwObject.Object.IpEnd));
                }
            }
            return result;
        }

        public string DisplayDestinationZone(Rule rule)
        {
            return (rule.DestinationZone != null ? rule.DestinationZone.Name : "");
        }

        public string DisplayDestination(Rule rule, string style = "", string location = "report", ReportType reportType = ReportType.Rules)
        {
            result = new StringBuilder();

            result.AppendLine("<p>");

            if (rule.DestinationNegated)
            {
                result.AppendLine(userConfig.GetText("anything_but") + " <br>");
            }
            string symbol = "";
            string nwobjLink = "";
            foreach (NetworkLocation destination in rule.Tos)
            {
                if (reportType == ReportType.Rules)
                {
                    if (destination.Object.Type.Name == "group")
                        symbol = "oi oi-list-rich";
                    else if (destination.Object.Type.Name == "network")
                        symbol = "oi oi-rss";
                    else if (destination.Object.Type.Name == "ip_range")
                        symbol = "oi oi-resize-width";
                    else
                        symbol = "oi oi-monitor";


                    string userLink = location == "" ? $"user{destination.User?.Id}"
                                                    : $"goto-report-m{rule.MgmtId}-user{destination.User?.Id}";

                    nwobjLink = location == "" ? $"nwobj{destination.Object.Id}"
                                                    : $"goto-report-m{rule.MgmtId}-nwobj{destination.Object.Id}";

                    if (destination.User != null)
                        result.AppendLine($"<span class=\"oi oi-people\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"{location}#{userLink}\" target=\"_top\" style=\"{style}\">{destination.User.Name}</a>@");

                    // string link = location == "" ? $"nwobj{destination.Object.Id}"
                    //                              : $"goto-report-m{rule.MgmtId}-nwobj{destination.Object.Id}";

                    result.Append($"<span class=\"{symbol}\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"{location}#{nwobjLink}\" target=\"_top\" style=\"{style}\">{destination.Object.Name}</a>");
                    result.Append(DisplayIpRange(destination.Object.IP, destination.Object.IpEnd));
                    result.AppendLine("<br>");
                }
                else if (reportType == ReportType.ResolvedRules)
                {
                    if (destination.Object.Type.Name == "group")
                        result.Append(resolveNetworkGroup(destination.Object.ObjectGroupFlats, location, rule, style));
                    else if (destination.Object.Type.Name == "network")
                        symbol = "oi oi-rss";
                    else if (destination.Object.Type.Name == "ip_range")
                        symbol = "oi oi-resize-width";
                    else
                        symbol = "oi oi-monitor";

                    string userLink = location == "" ? $"user{destination.User?.Id}"
                                                    : $"goto-report-m{rule.MgmtId}-user{destination.User?.Id}";

                    nwobjLink = location == "" ? $"nwobj{destination.Object.Id}"
                                                    : $"goto-report-m{rule.MgmtId}-nwobj{destination.Object.Id}";

                    if (destination.User != null)
                        result.AppendLine($"<span class=\"oi oi-people\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"{location}#{userLink}\" target=\"_top\" style=\"{style}\">{destination.User.Name}</a>@");
                    
                    result.Append($"<span class=\"{symbol}\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"{location}#{nwobjLink}\" target=\"_top\" style=\"{style}\">{destination.Object.Name}</a>");
                    result.Append(DisplayIpRange(destination.Object.IP, destination.Object.IpEnd));
                    result.AppendLine("<br>");
                }
            }
            result.AppendLine("</p>");
            return result.ToString();
        }

        public string DisplayIpRange(string Ip, string IpEnd)
        {
            return (Ip != null && Ip != "" ? $" ({Ip}{(IpEnd != null && IpEnd != "" && IpEnd != Ip ? $"-{IpEnd}" : "")})" : "");
        }

        public string DisplayService(Rule rule, string style = "", string location = "report", ReportType reportType = ReportType.Rules)
        {
            result = new StringBuilder();

            result.AppendLine("<p>");
            string link = "";
            if (rule.ServiceNegated)
            {
                result.AppendLine(userConfig.GetText("anything_but") + " <br>");
            }

            string symbol = "";
            foreach (ServiceWrapper service in rule.Services)
            {
                if (reportType == ReportType.Rules)
                {
                    if (service.Content.Type.Name == "group")
                        symbol = "oi oi-list-rich";
                    else
                        symbol = "oi oi-wrench";

                    link = location == "" ? $"svc{service.Content.Id}"
                                                : $"goto-report-m{rule.MgmtId}-svc{service.Content.Id}";

                    result.Append($"<span class=\"{symbol}\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"{location}#{link}\" target=\"_top\" style=\"{style}\">{service.Content.Name}</a>");

                    if (service.Content.DestinationPort != null)
                        result.Append(service.Content.DestinationPort == service.Content.DestinationPortEnd ? $" ({service.Content.DestinationPort}/{service.Content.Protocol?.Name})"
                            : $" ({service.Content.DestinationPort}-{service.Content.DestinationPortEnd}/{service.Content.Protocol?.Name})");
                    result.AppendLine("<br>");
                }
                else if (reportType == ReportType.ResolvedRules)
                {
                    symbol = "oi oi-wrench";
                    if (service.Content.Type.Name == "group")
                        result.Append(resolveNetworkServices(service.Content.ServiceGroupFlats, location, rule, style));
                    else
                    {
                        link = location == "" ? $"svc{service.Content.Id}"
                                                    : $"goto-report-m{rule.MgmtId}-svc{service.Content.Id}";

                        result.Append($"<span class=\"{symbol}\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"{location}#{link}\" target=\"_top\" style=\"{style}\">{service.Content.Name}</a>");

                        if (service.Content.DestinationPort != null)
                            result.Append(service.Content.DestinationPort == service.Content.DestinationPortEnd ? $" ({service.Content.DestinationPort}/{service.Content.Protocol?.Name})"
                                : $" ({service.Content.DestinationPort}-{service.Content.DestinationPortEnd}/{service.Content.Protocol?.Name})");
                        result.AppendLine("<br>");
                    }
                }
            }
            result.AppendLine("</p>");

            return result.ToString();
        }

        private StringBuilder resolveNetworkServices(GroupFlat<NetworkService>[] group, string location, Rule rule, string style)
        {
            string symbol = "";
            string nwobjLink = "";
            StringBuilder result = new StringBuilder();
            foreach (GroupFlat<NetworkService> nwService in group)
            {
                if (nwService.Object.Type.Name != "group")
                {
                    symbol = "oi oi-wrench";
                    nwobjLink = location == "" ? $"nwobj{nwService.Id}" : $"goto-report-m{rule.MgmtId}-nwobj{nwService.Id}";
                    result.Append($"<span class=\"{symbol}\">&nbsp;</span><a @onclick:stopPropagation=\"true\" href=\"{location}#{nwobjLink}\" target=\"_top\" style=\"{style}\">{nwService.Object.Name}</a><br>");
                    if (nwService.Object.DestinationPort != null)
                        result.Append(nwService.Object.DestinationPort == nwService.Object.DestinationPortEnd ? $" ({nwService.Object.DestinationPort}/{nwService.Object.Protocol?.Name})"
                            : $" ({nwService.Object.DestinationPort}-{nwService.Object.DestinationPortEnd}/{nwService.Object.Protocol?.Name})");
                }
            }
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
