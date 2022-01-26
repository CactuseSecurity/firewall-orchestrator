using FWO.Api.Data;
using FWO.Config.Api;
using System.Text;

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
            //result = new StringBuilder();
            //if (rules != null)
            //{
            //    int ruleNumber = Array.IndexOf(rules, rule) + 1;

            //    for (int i = 0; i < ruleNumber; i++)
            //        if (!string.IsNullOrEmpty(rules[i].SectionHeader))
            //            ruleNumber--;

            //    result.AppendLine($"{ruleNumber}");
            //}
            ////result.AppendLine($"DEBUG: {rule.OrderNumber}");
            //return result.ToString();
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

        public string DisplaySource(Rule rule, string style = "", bool recert = false)
        {
            result = new StringBuilder();

            result.AppendLine("<p>");

            if (rule.SourceNegated)
                result.AppendLine(userConfig.GetText("anything_but") + " <br>");

            string symbol = "";
            foreach (NetworkLocation source in rule.Froms)
            {
                if (source.Object.Type.Name == "group")
                    symbol = "oi oi-list-rich";
                else if (source.Object.Type.Name == "network")
                    symbol = "oi oi-rss";
                else
                    symbol = "oi oi-monitor";

                string page = recert ? "certification" : "report";

                if (source.User != null)
                    result.AppendLine($"<span class=\"oi oi-people\">&nbsp;</span><a href=\"{ page }#goto-report-m{rule.MgmtId}-user{source.User.Id}\" target=\"_top\" style=\"{style}\">{source.User.Name}</a>@");
                result.Append($"<span class=\"{symbol}\">&nbsp;</span><a href=\"{ page }#goto-report-m{rule.MgmtId}-nwobj{source.Object.Id}\" target=\"_top\" style=\"{style}\">{source.Object.Name}</a>");
                result.Append((source.Object.IP != null ? $" ({source.Object.IP})" : ""));
                result.AppendLine("<br>");
            }

            result.AppendLine("</p>");

            return result.ToString();
        }

        public string DisplayDestinationZone(Rule rule)
        {
            return (rule.DestinationZone != null ? rule.DestinationZone.Name : "");
        }

        public string DisplayDestination(Rule rule, string style = "", bool recert = false)
        {
            result = new StringBuilder();

            result.AppendLine("<p>");

            if (rule.DestinationNegated)
            {
                result.AppendLine(userConfig.GetText("anything_but") + " <br>");
            }

            string symbol = "";
            foreach (NetworkLocation destination in rule.Tos)
            {
                if (destination.Object.Type.Name == "group")
                    symbol = "oi oi-list-rich";
                else if (destination.Object.Type.Name == "network")
                    symbol = "oi oi-rss";
                else
                    symbol = "oi oi-monitor";

                string page = recert ? "certification" : "report";

                result.Append($"<span class=\"{symbol}\">&nbsp;</span><a href=\"{ page }#goto-report-m{rule.MgmtId}-nwobj{destination.Object.Id}\" target=\"_top\" style=\"{style}\">{destination.Object.Name}</a>");
                result.Append(destination.Object.IP != null ? $" ({destination.Object.IP})" : "");
                result.AppendLine("<br>");
            }

            result.AppendLine("</p>");

            return result.ToString();
        }

        public string DisplayService(Rule rule, string style = "", bool recert = false)
        {
            result = new StringBuilder();

            result.AppendLine("<p>");

            if (rule.ServiceNegated)
            {
                result.AppendLine(userConfig.GetText("anything_but") + " <br>");
            }

            string symbol = "";
            foreach (ServiceWrapper service in rule.Services)
            {
                if (service.Content.Type.Name == "group")
                    symbol = "oi oi-list-rich";
                else
                    symbol = "oi oi-wrench";

                string page = recert ? "certification" : "report";

                result.Append($"<span class=\"{symbol}\">&nbsp;</span><a href=\"{ page }#goto-report-m{rule.MgmtId}-svc{service.Content.Id}\" target=\"_top\" style=\"{style}\">{service.Content.Name}</a>");

                if (service.Content.DestinationPort != null)
                    result.Append(service.Content.DestinationPort == service.Content.DestinationPortEnd ? $" ({service.Content.DestinationPort}/{service.Content.Protocol?.Name})"
                        : $" ({service.Content.DestinationPort}-{service.Content.DestinationPortEnd}/{service.Content.Protocol?.Name})");
                result.AppendLine("<br>");
            }

            result.AppendLine("</p>");

            return result.ToString();
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
