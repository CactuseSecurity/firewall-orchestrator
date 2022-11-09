using FWO.Api.Data;
using FWO.Config.Api;
using System.Text;

namespace FWO.Ui.Display
{
    public class NatRuleDisplay : RuleDisplayHtml
    {
        public NatRuleDisplay(UserConfig userConfig) : base(userConfig)
        {}

        public string DisplayTranslatedSource(Rule rule, string style = "", string location = "report")
        {
            result = new StringBuilder();

            result.AppendLine("<p>");

            if (rule.NatData.TranslatedSourceNegated)
            {
                result.AppendLine(userConfig.GetText("anything_but") + " <br>");
            }

            string symbol = "";
            foreach (NetworkLocation source in rule.NatData.TranslatedFroms)
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

                string nwobjLink = location == "" ? $"nwobj{source.Object.Id}"
                                                  : $"goto-report-m{rule.MgmtId}-nwobj{source.Object.Id}";

                if (source.User != null)
                    result.AppendLine($"<span class=\"oi oi-people\">&nbsp;</span><a href=\"{location}#{userLink}\" target=\"_top\" style=\"{style}\">{source.User.Name}</a>@");
                result.Append($"<span class=\"{symbol}\">&nbsp;</span><a href=\"{location}#{nwobjLink}\" target=\"_top\" style=\"{style}\">{source.Object.Name}</a>");
                result.Append(" (");
                result.Append(DisplayIpRange(source.Object.IP, source.Object.IpEnd));
                result.Append(")");
                result.AppendLine("<br>");
            }
            result.AppendLine("</p>");

            return result.ToString();
        }

        public string DisplayTranslatedDestination(Rule rule, string style = "", string location = "report")
        {
            result = new StringBuilder();

            result.AppendLine("<p>");

            if (rule.NatData.TranslatedDestinationNegated)
            {
                result.AppendLine(userConfig.GetText("anything_but") + " <br>");
            }

            string symbol = "";
            foreach (NetworkLocation destination in rule.NatData.TranslatedTos)
            {
                if (destination.Object.Type.Name == "group")
                    symbol = "oi oi-list-rich";
                else if (destination.Object.Type.Name == "network")
                    symbol = "oi oi-rss";
                else if (destination.Object.Type.Name == "ip_range")
                    symbol = "oi oi-resize-width";
                else
                    symbol = "oi oi-monitor";

                string link = location == "" ? $"nwobj{destination.Object.Id}"
                                             : $"goto-report-m{rule.MgmtId}-nwobj{destination.Object.Id}";

                result.Append($"<span class=\"{symbol}\">&nbsp;</span><a href=\"{location}#{link}\" target=\"_top\" style=\"{style}\">{destination.Object.Name}</a>");
                result.Append(" (");
                result.Append(DisplayIpRange(destination.Object.IP, destination.Object.IpEnd));
                result.Append(")");
                result.AppendLine("<br>");
            }
            result.AppendLine("</p>");

            return result.ToString();
        }

        public string DisplayTranslatedService(Rule rule, string style = "", string location = "report")
        {
            result = new StringBuilder();

            result.AppendLine("<p>");

            if (rule.NatData.TranslatedServiceNegated)
            {
                result.AppendLine(userConfig.GetText("anything_but") + " <br>");
            }

            string symbol = "";
            foreach (ServiceWrapper service in rule.NatData.TranslatedServices)
            {
                if (service.Content.Type.Name == "group")
                    symbol = "oi oi-list-rich";
                else
                    symbol = "oi oi-wrench";

                string link = location == "" ? $"svc{service.Content.Id}"
                                             : $"goto-report-m{rule.MgmtId}-svc{service.Content.Id}";

                result.Append($"<span class=\"{symbol}\">&nbsp;</span><a href=\"{location}#{link}\" target=\"_top\" style=\"{style}\">{service.Content.Name}</a>");

                if (service.Content.DestinationPort != null)
                    result.Append(service.Content.DestinationPort == service.Content.DestinationPortEnd ? $" ({service.Content.DestinationPort}/{service.Content.Protocol?.Name})"
                        : $" ({service.Content.DestinationPort}-{service.Content.DestinationPortEnd}/{service.Content.Protocol?.Name})");
                result.AppendLine("<br>");
            }
            result.AppendLine("</p>");

            return result.ToString();
        }
    }
}
