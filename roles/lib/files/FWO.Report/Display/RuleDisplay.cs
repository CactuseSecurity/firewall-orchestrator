using FWO.Api.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Ui.Display
{
    public static class RuleDisplay
    {
        private static StringBuilder result;

        public static string DisplayNumber(this Rule rule, Rule[] rules)
        {
            result = new StringBuilder();
            if (rules != null)
            {
                int ruleNumber = Array.IndexOf(rules, rule) + 1;

                for (int i = 0; i < Array.IndexOf(rules, rule) + 1; i++)
                    if (!string.IsNullOrEmpty(rules[i].SectionHeader))
                        ruleNumber--;

                result.AppendLine($"{ruleNumber}");
            }
            //result.AppendLine($"DEBUG: {rule.OrderNumber}");
            return result.ToString();
        }

        public static string DisplayName(this Rule rule)
        {
            return rule.Name;
        }

        public static string DisplaySourceZone(this Rule rule)
        {
            return rule.SourceZone?.Name;
        }

        public static string DisplaySource(this Rule rule)
        {
            result = new StringBuilder();

            result.AppendLine("<p>");

            if (rule.SourceNegated)
                result.AppendLine("anything but <br>");

            string symbol = "";
            foreach (NetworkLocation source in rule.Froms)
            {
                if (source.Object.Type.Name == "group")
                    symbol = "oi oi-list-rich";
                else if (source.Object.Type.Name == "network")
                    symbol = "oi oi-rss";
                else
                    symbol = "oi oi-monitor";

                if (source.User != null)
                    result.AppendLine($"<span class=\"oi oi-people\">&nbsp;</span><a href=\"report#user{source.User.Id}\" target=\"_top\">{source.User.Name}</a>@");
                result.Append($"<span class=\"{symbol}\">&nbsp;</span><a href=\"report#nwobj{source.Object.Id}\" target=\"_top\">{source.Object.Name}</a>");
                result.Append((source.Object.IP != null ? $" ({source.Object.IP})" : ""));
                result.AppendLine("<br>");
            }

            result.AppendLine("</p>");

            return result.ToString();
        }

        public static string DisplayDestinationZone(this Rule rule)
        {
            return rule.DestinationZone?.Name;
        }

        public static string DisplayDestination(this Rule rule)
        {
            result = new StringBuilder();

            result.AppendLine("<p>");

            if (rule.SourceNegated)
            {
                result.AppendLine("anything but <br>");
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

                result.Append($"<span class=\"{symbol}\">&nbsp;</span><a href=\"report#nwobj{destination.Object.Id}\" target=\"_top\">{destination.Object.Name}</a>");
                result.Append(destination.Object.IP != null ? $" ({destination.Object.IP})" : "");
                result.AppendLine("<br>");
            }

            result.AppendLine("</p>");

            return result.ToString();
        }

        public static string DisplayService(this Rule rule)
        {
            result = new StringBuilder();

            result.AppendLine("<p>");

            if (rule.ServiceNegated)
            {
                result.AppendLine("anything but <br>");
            }

            string symbol = "";
            foreach (ServiceWrapper service in rule.Services)
            {
                if (service.Content.Type.Name == "group")
                    symbol = "oi oi-list-rich";
                else
                    symbol = "oi oi-wrench";

                result.Append($"<span class=\"{symbol}\">&nbsp;</span><a href=\"report/#svc{service.Content.Id}\" target=\"_top\">{service.Content.Name}</a>");

                if (service.Content.DestinationPort != null)
                    result.Append(service.Content.DestinationPort == service.Content.DestinationPortEnd ? $" ({service.Content.DestinationPort}/{service.Content.Protocol?.Name})"
                        : $" ({service.Content.DestinationPort}-{service.Content.DestinationPortEnd}/{service.Content.Protocol?.Name})");
                result.AppendLine("<br>");
            }

            result.AppendLine("</p>");

            return result.ToString();
        }

        public static string DisplayAction(this Rule rule)
        {
            return rule.Action;
        }

        public static string DisplayTrack(this Rule rule)
        {
            return rule.Track;
        }

        public static string DisplayEnabled(this Rule rule, bool export = false)
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

        public static string DisplayUid(this Rule rule)
        {
            return rule.Uid;
        }

        public static string DisplayComment(this Rule rule)
        {
            return rule.Comment;
        }
    }
}
