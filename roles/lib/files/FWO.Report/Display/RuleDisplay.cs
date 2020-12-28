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
                result.AppendLine($"{Array.IndexOf(rules, rule) + 1} <br>");
            result.AppendLine($"DEBUG: {rule.OrderNumber}");
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
            {
                result.AppendLine("anything but <br>");
            }

            foreach (NetworkLocation source in rule.Froms)
            {
                if (source.User != null)
                {
                    result.AppendLine($"<a href=\"report#user{source.User.Id}\" target=\"_top\">{source.User.Name}</a>@");
                }

                result.Append($"<a href=\"report#nwobj{source.Object.Id}\" target=\"_top\">{source.Object.Name}</a>");
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

            foreach (NetworkLocation destination in rule.Tos)
            {
                result.Append($"<a href=\"report#nwobj{destination.Object.Id}\" target=\"_top\">{destination.Object.Name}</a>");
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

            foreach (ServiceWrapper service in rule.Services)
            {
                result.Append($"<a href=\"report#svc{service.Content.Id}\" target=\"_top\">{service.Content.Name}</a>");

                // result.Append(service.Content.DestinationPort != null ? $" ({service.Content.DestinationPort}/{service.Content.Protocol.Name})" : "");
                string protoName = "";
                if (service.Content.Protocol != null && service.Content.Protocol.Name != null) protoName = service.Content.Protocol.Name;
                result.Append(service.Content.DestinationPort != null ? $" ({service.Content.DestinationPort}/{protoName})" : "");
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

        public static string DisplayDisabled(this Rule rule)
        {
            return $"<div class=\"oi {(rule.Disabled ? "oi-check" : "oi-x")}\"></div>";
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
